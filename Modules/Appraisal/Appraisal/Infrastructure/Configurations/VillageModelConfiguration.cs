using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class VillageModelConfiguration : IEntityTypeConfiguration<VillageModel>
{
    public void Configure(EntityTypeBuilder<VillageModel> builder)
    {
        builder.ToTable("VillageModels");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId);

        // Model Info
        builder.Property(e => e.ModelName).HasMaxLength(200);
        builder.Property(e => e.ModelDescription).HasMaxLength(500);
        builder.Property(e => e.StartingPrice).HasPrecision(18, 2);

        // Usable Area
        builder.Property(e => e.UsableAreaMin).HasPrecision(10, 2);
        builder.Property(e => e.UsableAreaMax).HasPrecision(10, 2);
        builder.Property(e => e.StandardUsableArea).HasPrecision(10, 2);

        // Land Area
        builder.Property(e => e.LandAreaRai).HasPrecision(10, 4);
        builder.Property(e => e.LandAreaNgan).HasPrecision(10, 4);
        builder.Property(e => e.LandAreaWa).HasPrecision(10, 4);
        builder.Property(e => e.StandardLandArea).HasPrecision(10, 4);

        // Insurance
        builder.Property(e => e.FireInsuranceCondition).HasMaxLength(200);

        // Building Info
        builder.Property(e => e.BuildingType).HasMaxLength(100);
        builder.Property(e => e.BuildingTypeOther).HasMaxLength(4000);
        builder.Property(e => e.NumberOfFloors).HasPrecision(5, 1);
        builder.Property(e => e.DecorationType).HasMaxLength(100);
        builder.Property(e => e.DecorationTypeOther).HasMaxLength(4000);
        builder.Property(e => e.EncroachingOthersRemark).HasMaxLength(4000);
        builder.Property(e => e.EncroachingOthersArea).HasPrecision(18, 4);

        // Construction Details
        builder.Property(e => e.BuildingMaterialType).HasMaxLength(100);
        builder.Property(e => e.BuildingStyleType).HasMaxLength(100);
        builder.Property(e => e.ResidentialRemark).HasMaxLength(4000);
        builder.Property(e => e.ConstructionStyleType).HasMaxLength(100);
        builder.Property(e => e.ConstructionStyleRemark).HasMaxLength(4000);

        // Structure Components (JSON arrays)
        builder.Property(e => e.StructureType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.StructureTypeOther).HasMaxLength(4000);

        builder.Property(e => e.RoofFrameType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoofFrameTypeOther).HasMaxLength(4000);

        builder.Property(e => e.RoofType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoofTypeOther).HasMaxLength(4000);

        builder.Property(e => e.CeilingType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.CeilingTypeOther).HasMaxLength(4000);

        builder.Property(e => e.InteriorWallType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.InteriorWallTypeOther).HasMaxLength(4000);

        builder.Property(e => e.ExteriorWallType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.ExteriorWallTypeOther).HasMaxLength(4000);

        builder.Property(e => e.FenceType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.FenceTypeOther).HasMaxLength(4000);

        builder.Property(e => e.ConstructionType).HasMaxLength(100);
        builder.Property(e => e.ConstructionTypeOther).HasMaxLength(4000);

        // Utilization
        builder.Property(e => e.UtilizationType).HasMaxLength(100);
        builder.Property(e => e.UtilizationTypeOther).HasMaxLength(4000);
        // Documents (JSON)
        builder.Property(e => e.ImageDocumentIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(2000)");

        // Other
        builder.Property(e => e.Remark).HasMaxLength(4000);

        // Area Details (owned collection)
        builder.OwnsMany(e => e.AreaDetails, areaDetail =>
        {
            areaDetail.ToTable("VillageModelAreaDetails");
            areaDetail.WithOwner().HasForeignKey("VillageModelId");
            areaDetail.HasKey("Id");
            areaDetail.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            areaDetail.Property(p => p.AreaDescription).HasMaxLength(200).HasColumnName("AreaDescription");
            areaDetail.Property(p => p.AreaSize).HasPrecision(10, 2).HasColumnName("AreaSize");
        });

        builder.Navigation(e => e.AreaDetails)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Surfaces (owned collection)
        builder.OwnsMany(e => e.Surfaces, surface =>
        {
            surface.ToTable("VillageModelSurfaces");
            surface.WithOwner().HasForeignKey("VillageModelId");
            surface.HasKey("Id");
            surface.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            surface.Property(s => s.FloorType).HasMaxLength(100);
            surface.Property(s => s.FloorStructureType).HasMaxLength(100);
            surface.Property(s => s.FloorStructureTypeOther).HasMaxLength(200);
            surface.Property(s => s.FloorSurfaceType).HasMaxLength(100);
            surface.Property(s => s.FloorSurfaceTypeOther).HasMaxLength(200);
        });

        builder.Navigation(e => e.Surfaces)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Depreciation Details (owned collection)
        builder.OwnsMany(e => e.DepreciationDetails, dep =>
        {
            dep.ToTable("VillageModelDepreciationDetails");
            dep.WithOwner().HasForeignKey("VillageModelId");
            dep.HasKey("Id");
            dep.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            dep.Property(d => d.AreaDescription).HasMaxLength(200);
            dep.Property(d => d.Area).HasPrecision(18, 4);
            dep.Property(d => d.DepreciationMethod).HasMaxLength(100);
            dep.Property(d => d.PricePerSqMBeforeDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.PriceBeforeDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.PricePerSqMAfterDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.PriceAfterDepreciation).HasPrecision(18, 2);
            dep.Property(d => d.DepreciationYearPct).HasPrecision(10, 4);
            dep.Property(d => d.TotalDepreciationPct).HasPrecision(10, 4);
            dep.Property(d => d.PriceDepreciation).HasPrecision(18, 2);

            // DepreciationPeriods (nested owned collection)
            dep.OwnsMany(d => d.DepreciationPeriods, period =>
            {
                period.ToTable("VillageModelDepreciationPeriods");
                period.WithOwner().HasForeignKey("VillageModelDepreciationDetailId");
                period.HasKey("Id");
                period.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
                period.Property(p => p.DepreciationPerYear).HasPrecision(10, 4);
                period.Property(p => p.TotalDepreciationPct).HasPrecision(10, 4);
                period.Property(p => p.PriceDepreciation).HasPrecision(18, 2);
            });

            dep.Navigation(d => d.DepreciationPeriods)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(e => e.DepreciationDetails)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
