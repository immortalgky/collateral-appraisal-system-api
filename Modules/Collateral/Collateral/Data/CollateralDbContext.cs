using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Reappraisal;
using Shared.Data.Outbox;
using Shared.Scheduling;

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
    public DbSet<CollateralResultLog> CollateralResultLogs => Set<CollateralResultLog>();
    public DbSet<PendingCollateralResult> PendingCollateralResults => Set<PendingCollateralResult>();
    public DbSet<CollateralDocument> CollateralDocuments => Set<CollateralDocument>();
    public DbSet<BlockReappraisalDue> BlockReappraisalDue => Set<BlockReappraisalDue>();
    public DbSet<ProjectUnit> ProjectUnits => Set<ProjectUnit>();
    public DbSet<ReappraisalCandidate> ReappraisalCandidates => Set<ReappraisalCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("collateral");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.AddIntegrationEventOutbox();

        // Per-module recurring-job schedule table (collateral.JobSchedules)
        modelBuilder.AddJobSchedules();

        base.OnModelCreating(modelBuilder);
    }
}
