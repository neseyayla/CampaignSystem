using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Data;

/// <summary>
/// EF Core session against the campaign database.
///
/// The context itself holds no mapping rules. Every table, column, index and relationship
/// is declared in its own <see cref="IEntityTypeConfiguration{TEntity}"/> class under
/// Data/Configurations, which keeps this file stable as the schema grows.
/// </summary>
public class CampaignDbContext : DbContext
{
    public CampaignDbContext(DbContextOptions<CampaignDbContext> options)
        : base(options)
    {
    }

    // Lookup tables
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<TransactionCode> TransactionCodes => Set<TransactionCode>();

    // Customer and card
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Card> Cards => Set<Card>();

    // Campaign definition
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    // Campaign criteria
    public DbSet<CampaignSegment> CampaignSegments => Set<CampaignSegment>();
    public DbSet<CampaignProduct> CampaignProducts => Set<CampaignProduct>();
    public DbSet<CampaignMerchant> CampaignMerchants => Set<CampaignMerchant>();
    public DbSet<CampaignTransactionCode> CampaignTransactionCodes => Set<CampaignTransactionCode>();

    // Enrollment, transactions and results
    public DbSet<CampaignParticipation> CampaignParticipations => Set<CampaignParticipation>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CampaignReward> CampaignRewards => Set<CampaignReward>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampaignDbContext).Assembly);
    }
}
