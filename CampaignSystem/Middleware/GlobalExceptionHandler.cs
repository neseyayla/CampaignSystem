using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Middleware;

/// <summary>
/// The last line of defence for anything a controller or service does not handle itself.
/// Logs the exception once — with the request method and path — and returns a plain
/// <see cref="ProblemDetails"/> 500 so the caller gets a clean shape instead of a stack
/// trace. Registered with AddExceptionHandler in Program.cs and run by UseExceptionHandler.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Beklenmeyen bir hata oluştu."
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        // The exception is fully handled here; nothing further should try to process it.
        return true;
    }
}
