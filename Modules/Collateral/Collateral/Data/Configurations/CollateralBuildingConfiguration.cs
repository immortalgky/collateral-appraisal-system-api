using Collateral.CollateralProperties.Models;

namespace Collateral.Data.Configurations;

public class CollateralBuildingConfiguration : IEntityTypeConfiguration<CollateralBuilding>
{
    public void Configure(EntityTypeBuilder<CollateralBuilding> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn().HasColumnName("BuildingId");

        builder.Property(p => p.BuildingNo).UseBuildingNoConfig();

        builder.Property(p => p.ModelName).UseTinyStringConfig();

        builder.Property(p => p.HouseNo).UseTinyStringConfig();

        builder.Property(p => p.BuiltOnTitleNo).UseBuildOnTitleNoConfig();

        builder.Property(p => p.Owner).HasMaxLength(30);
    }
}
