using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralEngagementBuildingConfiguration : IEntityTypeConfiguration<CollateralEngagementBuilding>
{
    public void Configure(EntityTypeBuilder<CollateralEngagementBuilding> builder)
    {
        builder.ToTable("CollateralEngagementBuildings");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.EngagementId).IsRequired();
        builder.Property(b => b.BuildingTypeCode).IsRequired().HasMaxLength(10);
        builder.Property(b => b.BuildingArea).HasPrecision(18, 4);
        builder.Property(b => b.BuildingValue).HasPrecision(18, 2);
        builder.Property(b => b.Sequence).IsRequired();

        // Search filter: EXISTS (... WHERE ceb.EngagementId = e.Id AND ceb.BuildingTypeCode IN @codes)
        builder.HasIndex(b => new { b.EngagementId, b.BuildingTypeCode })
            .HasDatabaseName("IX_CollateralEngagementBuildings_Engagement_TypeCode");

        // Engagement fetch
        builder.HasIndex(b => b.EngagementId)
            .HasDatabaseName("IX_CollateralEngagementBuildings_EngagementId");
    }
}
