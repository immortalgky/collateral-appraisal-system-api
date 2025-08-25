using Appraisal.AppraisalProperties.Models;

namespace Appraisal.Data;

public class AppraisalDbContext : DbContext
{
    public AppraisalDbContext(DbContextOptions<AppraisalDbContext> options) : base(options)
    {
    }

    public DbSet<RequestAppraisal> Appraisals => Set<RequestAppraisal>();
    public DbSet<LandAppraisalDetail> LandAppraisalDetails => Set<LandAppraisalDetail>();
    public DbSet<BuildingAppraisalDetail> BuildingAppraisalDetails => Set<BuildingAppraisalDetail>();
    public DbSet<CondoAppraisalDetail> CondoAppraisalDetails => Set<CondoAppraisalDetail>();
    public DbSet<MachineAppraisalDetail> MachineAppraisalDetails => Set<MachineAppraisalDetail>();
    public DbSet<MachineAppraisalAdditionalInfo> MachineAppraisalAdditionalInfos => Set<MachineAppraisalAdditionalInfo>();
    public DbSet<VehicleAppraisalDetail> VehicleAppraisalDetails => Set<VehicleAppraisalDetail>();
    public DbSet<VesselAppraisalDetail> VesselAppraisalDetails => Set<VesselAppraisalDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Appraisal");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}