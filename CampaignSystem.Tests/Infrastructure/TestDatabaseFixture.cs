using CampaignSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Tests.Infrastructure;

/// <summary>
/// Provides a real SQL Server database for the tests to run against.
///
/// LocalDB rather than the in-memory provider on purpose: half of the model is SQL Server
/// specific — the filtered unique index on Rrn, varchar columns, the composite keys — and
/// the in-memory provider enforces none of it. A test that passes against a fake database
/// and fails against the real one is worse than no test.
///
/// The database is dropped and rebuilt once per test run. Rebuilding through Migrate rather
/// than EnsureCreated means the tests exercise the same migrations that production does,
/// and the seeded reference data arrives with them.
/// </summary>
public class TestDatabaseFixture
{
    // On a machine with SQL Server Express but no LocalDB the whole suite fails to initialise.
    // Read the connection string from CAMPAIGNSYSTEM_TEST_CONNECTION when it is set, falling
    // back to the LocalDB string otherwise — no change for anyone already on LocalDB.
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("CAMPAIGNSYSTEM_TEST_CONNECTION")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=CampaignSystem_Test;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly Lock InitialisationLock = new();
    private static bool _initialised;

    public TestDatabaseFixture()
    {
        lock (InitialisationLock)
        {
            if (_initialised)
            {
                return;
            }

            using var context = CreateContext();

            context.Database.EnsureDeleted();
            context.Database.Migrate();

            _initialised = true;
        }
    }

    public CampaignDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CampaignDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);
}
