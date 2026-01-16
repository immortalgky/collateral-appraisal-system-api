using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class BuildingAppraisalDetailConfiguration : IEntityTypeConfiguration<BuildingAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<BuildingAppraisalDetail> builder)
    {
        builder.ToTable("BuildingAppraisalDetails", "appraisal");
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
        builder.Property(e => e.OwnerName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ObligationDetails).HasMaxLength(500);

        // Building Status
        builder.Property(e => e.BuildingConditionType).HasMaxLength(50);
        builder.Property(e => e.ConstructionCompletionPercent).HasPrecision(5, 2);

        // Building Info
        builder.Property(e => e.BuildingType).HasMaxLength(100);
        builder.Property(e => e.BuildingTypeOther).HasMaxLength(200);
        builder.Property(e => e.DecorationType).HasMaxLength(100);
        builder.Property(e => e.DecorationTypeOther).HasMaxLength(200);
        builder.Property(e => e.EncroachmentRemark).HasMaxLength(500);
        builder.Property(e => e.EncroachmentArea).HasPrecision(18, 4);

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
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.StructureTypeOther).HasMaxLength(200);
        builder.Property(e => e.RoofFrameType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoofFrameTypeOther).HasMaxLength(200);
        builder.Property(e => e.RoofType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoofTypeOther).HasMaxLength(200);
        builder.Property(e => e.CeilingType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.CeilingTypeOther).HasMaxLength(200);
        builder.Property(e => e.InteriorWallType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.InteriorWallTypeOther).HasMaxLength(200);
        builder.Property(e => e.ExteriorWallType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.ExteriorWallTypeOther).HasMaxLength(200);
        builder.Property(e => e.FenceType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
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
    }
}