using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class
    BuildingAppraisalDetailConfiguration : IOwnedEntityConfiguration<AppraisalProperty, BuildingAppraisalDetail>
{
    public void Configure(OwnedNavigationBuilder<AppraisalProperty, BuildingAppraisalDetail> builder)
    {
        builder.ToTable("BuildingAppraisalDetails", "appraisal");
        builder.WithOwner().HasForeignKey(e => e.AppraisalPropertyId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Property Identification
        builder.Property(e => e.PropertyName).HasMaxLength(200);
        builder.Property(e => e.BuildingNumber).HasMaxLength(50);
        builder.Property(e => e.ModelName).HasMaxLength(100);
        builder.Property(e => e.BuiltOnTitleNumber).HasMaxLength(100);
        builder.Property(e => e.HouseNumber).HasMaxLength(50);

        // Owner
        builder.Property(e => e.OwnerName).HasMaxLength(200); // should not require for land and building
        builder.Property(e => e.ObligationDetails).HasMaxLength(500);

        // Building Status
        builder.Property(e => e.BuildingConditionType).HasMaxLength(50);
        builder.Property(e => e.ConstructionCompletionPercent).HasPrecision(5, 2);

        // Building Info
        builder.Property(e => e.NumberOfFloors).HasPrecision(5, 2);
        builder.Property(e => e.BuildingType).HasMaxLength(100);
        builder.Property(e => e.BuildingTypeOther).HasMaxLength(200);
        builder.Property(e => e.DecorationType).HasMaxLength(100);
        builder.Property(e => e.DecorationTypeOther).HasMaxLength(200);
        builder.Property(e => e.EncroachingOthersRemark).HasMaxLength(500);
        builder.Property(e => e.EncroachingOthersArea).HasPrecision(18, 4);

        // Construction Details
        builder.Property(e => e.BuildingMaterialType).HasMaxLength(100);
        builder.Property(e => e.BuildingStyleType).HasMaxLength(100);
        builder.Property(e => e.ResidentialRemark).HasMaxLength(200);
        builder.Property(e => e.ConstructionStyleType).HasMaxLength(100);
        builder.Property(e => e.ConstructionStyleRemark).HasMaxLength(500);

        // Structure Components
        builder.Property(e => e.StructureType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.StructureTypeOther).HasMaxLength(200);
        builder.Property(e => e.RoofFrameType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoofFrameTypeOther).HasMaxLength(200);
        builder.Property(e => e.RoofType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoofTypeOther).HasMaxLength(200);
        builder.Property(e => e.CeilingType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.CeilingTypeOther).HasMaxLength(200);
        builder.Property(e => e.InteriorWallType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.InteriorWallTypeOther).HasMaxLength(200);
        builder.Property(e => e.ExteriorWallType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.ExteriorWallTypeOther).HasMaxLength(200);
        builder.Property(e => e.FenceType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.FenceTypeOther).HasMaxLength(200);
        builder.Property(e => e.ConstructionType).HasMaxLength(100);
        builder.Property(e => e.ConstructionTypeOther).HasMaxLength(200);

        // Utilization
        builder.Property(e => e.UtilizationType).HasMaxLength(100);
        builder.Property(e => e.UtilizationTypeOther).HasMaxLength(200);

        // Area & Pricing
        builder.Property(e => e.TotalBuildingArea).HasPrecision(18, 4);
        builder.Property(e => e.BuildingInsurancePrice).HasPrecision(18, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);
        builder.Property(e => e.ForcedSalePrice).HasPrecision(18, 2);

        // Other
        builder.Property(e => e.Remark).HasMaxLength(1000);

        // Relationship - FK to AppraisalProperty (1:1)
        builder.Property(e => e.AppraisalPropertyId).IsRequired();

        // DepreciationDetails - Owned collection
        builder.OwnsMany(e => e.DepreciationDetails, dep =>
        {
            dep.ToTable("BuildingDepreciationDetails", "appraisal");
            dep.WithOwner().HasForeignKey(d => d.BuildingAppraisalDetailId);
            dep.HasKey(d => d.Id);
            dep.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            dep.Property(d => d.AreaDescription).HasMaxLength(200);
            dep.Property(d => d.Area).HasPrecision(18, 4);
            dep.Property(d => d.PricePerSqMBeforeDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.PriceBeforeDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.PricePerSqMAfterDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.PriceAfterDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.DepreciationMethod).IsRequired().HasMaxLength(20);
            dep.Property(d => d.DepreciationYearPct).HasPrecision(7, 4);
            dep.Property(d => d.TotalDepreciationPct).HasPrecision(7, 4);
            dep.Property(d => d.PriceDepreciation).HasPrecision(18, 2);

            dep.HasIndex(d => d.BuildingAppraisalDetailId);

            // Nested OwnsMany for DepreciationPeriods
            dep.OwnsMany(d => d.DepreciationPeriods, period =>
            {
                period.ToTable("BuildingDepreciationPeriods", "appraisal");
                period.WithOwner().HasForeignKey(p => p.BuildingDepreciationDetailId);
                period.HasKey(p => p.Id);
                period.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

                period.Property(p => p.DepreciationPerYear).HasPrecision(7, 4);
                period.Property(p => p.TotalDepreciationPct).HasPrecision(7, 4);
                period.Property(p => p.PriceDepreciation).HasPrecision(18, 2);

                period.HasIndex(p => p.BuildingDepreciationDetailId);
            });
        });

        // Surfaces - Owned collection
        builder.OwnsMany(e => e.Surfaces, surf =>
        {
            surf.ToTable("BuildingAppraisalSurfaces", "appraisal");
            surf.WithOwner().HasForeignKey(s => s.BuildingAppraisalDetailId);
            surf.HasKey(s => s.Id);
            surf.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            surf.Property(s => s.FromFloorNumber).IsRequired();
            surf.Property(s => s.ToFloorNumber).IsRequired();
            surf.Property(s => s.FloorType).HasMaxLength(50);
            surf.Property(s => s.FloorStructureType).HasMaxLength(50);
            surf.Property(s => s.FloorStructureTypeOther).HasMaxLength(200);
            surf.Property(s => s.FloorSurfaceType).HasMaxLength(50);
            surf.Property(s => s.FloorSurfaceTypeOther).HasMaxLength(200);

            surf.HasIndex(s => s.BuildingAppraisalDetailId);
        });
    }
}