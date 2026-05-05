using Collateral.CollateralMasters.Models;
using Shared.Data.Outbox;

namespace Collateral.Data;

public class CollateralDbContext : DbContext
{
    public CollateralDbContext(DbContextOptions<CollateralDbContext> options) : base(options)
    {
    }

    public DbSet<CollateralMaster> CollateralMasters => Set<CollateralMaster>();
    public DbSet<CollateralEngagement> CollateralEngagements => Set<CollateralEngagement>();
    public DbSet<CollateralMasterAuditLog> CollateralMasterAuditLogs => Set<CollateralMasterAuditLog>();
    public DbSet<CollateralBackfillReport> CollateralBackfillReports => Set<CollateralBackfillReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("collateral");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.AddIntegrationEventInbox();

        base.OnModelCreating(modelBuilder);
    }
}
