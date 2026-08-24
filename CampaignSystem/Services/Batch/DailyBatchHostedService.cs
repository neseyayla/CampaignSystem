using CampaignSystem.Configuration;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services;

/// <summary>
/// Runs <see cref="IDailyBatchService"/> once a day at the configured time.
///
/// The logic itself lives in the service, not here: the endpoint and this class are two
/// doors into the same room. Swapping this out for an external scheduler changes nothing
/// about what the batch does.
/// </summary>
public class DailyBatchHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DailyBatchOptions> options,
    ILogger<DailyBatchHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation(
                "Daily batch hosted service is disabled; an external scheduler is expected to call the endpoint.");
            return;
        }

        logger.LogInformation("Daily batch scheduled for {RunAt} local time.", options.Value.RunAt);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(DateTime.Now, options.Value.RunAt);

            logger.LogInformation("Next daily batch run in {Hours:F1} hours.", delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // The application is shutting down.
                return;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        // A hosted service is a singleton, so it cannot hold the scoped DbContext the batch
        // needs. A scope per run also means one run's tracked entities never leak into the next.
        using var scope = scopeFactory.CreateScope();

        var batch = scope.ServiceProvider.GetRequiredService<IDailyBatchService>();

        try
        {
            await batch.RunAsync(stoppingToken);
        }
        catch (Exception exception)
        {
            // Never let a failed run kill the loop: tomorrow's campaigns still need loading.
            logger.LogError(exception, "The daily batch run failed. The next run is still scheduled.");
        }
    }

    /// <summary>
    /// How long until the next occurrence of <paramref name="runAt"/>. When the time has
    /// already passed today, the wait runs to tomorrow rather than firing immediately —
    /// restarting the application should not trigger a batch run.
    /// </summary>
    private static TimeSpan TimeUntilNextRun(DateTime now, TimeOnly runAt)
    {
        var next = now.Date.Add(runAt.ToTimeSpan());

        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
