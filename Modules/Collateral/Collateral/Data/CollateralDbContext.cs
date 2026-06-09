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
    public DbSet<CollateralEngagementBuilding> CollateralEngagementBuildings => Set<CollateralEngagementBuilding>();
    public DbSet<CollateralMasterAuditLog> CollateralMasterAuditLogs => Set<CollateralMasterAuditLog>();
    public DbSet<CollateralBackfillReport> CollateralBackfillReports => Set<CollateralBackfillReport>();
    public DbSet<CollateralDocument> CollateralDocuments => Set<CollateralDocument>();
    public DbSet<BlockReappraisalDue> BlockReappraisalDue => Set<BlockReappraisalDue>();
    public DbSet<ProjectUnit> ProjectUnits => Set<ProjectUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("collateral");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.AddIntegrationEventInbox();

        base.OnModelCreating(modelBuilder);
    }
}
