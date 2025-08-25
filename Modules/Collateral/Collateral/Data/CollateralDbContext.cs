using Collateral.CollateralProperties.Models;

namespace Collateral.Data;

public class CollateralDbContext : DbContext
{
    public CollateralDbContext(DbContextOptions<CollateralDbContext> options) : base(options)
    {
    }

    public DbSet<CollateralMaster> CollateralMasters => Set<CollateralMaster>();
    public DbSet<CollateralMachine> CollateralMachines => Set<CollateralMachine>();
    public DbSet<CollateralVehicle> CollateralVehicles => Set<CollateralVehicle>();
    public DbSet<CollateralVessel> CollateralVessels => Set<CollateralVessel>();
    public DbSet<CollateralLand> CollateralLands => Set<CollateralLand>();
    public DbSet<CollateralBuilding> CollateralBuildings => Set<CollateralBuilding>();
    public DbSet<CollateralCondo> CollateralCondos => Set<CollateralCondo>();
    public DbSet<LandTitle> LandTitles => Set<LandTitle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the default schema for the database
        modelBuilder.HasDefaultSchema("collateral");

        // Apply global conventions for the model
        modelBuilder.ApplyGlobalConventions();

        // Apply configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Call the base method to ensure any additional configurations are applied
        base.OnModelCreating(modelBuilder);
    }
}   