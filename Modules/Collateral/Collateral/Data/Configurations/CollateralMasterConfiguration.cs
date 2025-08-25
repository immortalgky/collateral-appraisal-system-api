namespace Collateral.Data.Configurations;

public class CollateralMasterConfigurations : IEntityTypeConfiguration<CollateralMaster>
{
    public void Configure(EntityTypeBuilder<CollateralMaster> builder)
    {
        builder.Property(p => p.Id).HasColumnName("CollatId");
        builder.Property(p => p.CollatType).UseCodeConfig();

        builder.HasOne(p => p.CollateralLand)
            .WithOne()
            .HasForeignKey<CollateralLand>(p => p.CollatId)
            .IsRequired();

        builder.HasOne(p => p.LandTitle)
            .WithOne()
            .HasForeignKey<LandTitle>(p => p.CollatId)
            .IsRequired();

        builder.HasOne(p => p.CollateralBuilding)
            .WithOne()
            .HasForeignKey<CollateralBuilding>(p => p.CollatId)
            .IsRequired();

        builder.HasOne(p => p.CollateralCondo)
            .WithOne()
            .HasForeignKey<CollateralCondo>(p => p.CollatId)
            .IsRequired();

        builder.HasOne(p => p.CollateralMachine)
            .WithOne()
            .HasForeignKey<CollateralMachine>(p => p.CollatId)
            .IsRequired();

        builder.HasOne(p => p.CollateralVehicle)
            .WithOne()
            .HasForeignKey<CollateralVehicle>(p => p.CollatId)
            .IsRequired();

        builder.HasOne(p => p.CollateralVessel)
            .WithOne()
            .HasForeignKey<CollateralVessel>(p => p.CollatId)
            .IsRequired();
    }
}