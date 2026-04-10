using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class CondoTowerConfiguration : IEntityTypeConfiguration<CondoTower>
{
    public void Configure(EntityTypeBuilder<CondoTower> builder)
    {
        builder.ToTable("CondoTowers");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId);

        // Tower Identification
        builder.Property(e => e.TowerName).HasMaxLength(200);
        builder.Property(e => e.CondoRegistrationNumber).HasMaxLength(100);

        // Condition & Obligation
        builder.Property(e => e.ConditionType).HasMaxLength(100);
        builder.Property(e => e.ObligationDetails).HasMaxLength(500);
        builder.Property(e => e.DocumentValidationType).HasMaxLength(100);

        // Location
        builder.Property(e => e.Distance).HasPrecision(10, 2);
        builder.Property(e => e.RoadWidth).HasPrecision(10, 2);
        builder.Property(e => e.RightOfWay).HasPrecision(10, 2);
        builder.Property(e => e.RoadSurfaceType).HasMaxLength(100);
        builder.Property(e => e.RoadSurfaceTypeOther).HasMaxLength(4000);

        // Decoration
        builder.Property(e => e.DecorationType).HasMaxLength(100);
        builder.Property(e => e.DecorationTypeOther).HasMaxLength(4000);

        // Building Info
        builder.Property(e => e.BuildingFormType).HasMaxLength(100);
        builder.Property(e => e.ConstructionMaterialType).HasMaxLength(100);

        // Materials
        builder.Property(e => e.GroundFloorMaterialType).HasMaxLength(100);
        builder.Property(e => e.GroundFloorMaterialTypeOther).HasMaxLength(4000);
        builder.Property(e => e.UpperFloorMaterialType).HasMaxLength(100);
        builder.Property(e => e.UpperFloorMaterialTypeOther).HasMaxLength(4000);
        builder.Property(e => e.BathroomFloorMaterialType).HasMaxLength(100);
        builder.Property(e => e.BathroomFloorMaterialTypeOther).HasMaxLength(4000);
        builder.Property(e => e.RoofTypeOther).HasMaxLength(4000);

        // JSON columns
        builder.Property(e => e.ModelTypeIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(2000)");

        builder.Property(e => e.RoofType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");

        builder.Property(e => e.ImageDocumentIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(2000)");

        // Legal Restrictions
        builder.Property(e => e.ExpropriationRemark).HasMaxLength(4000);
        builder.Property(e => e.RoyalDecree).HasMaxLength(500);
        builder.Property(e => e.ForestBoundaryRemark).HasMaxLength(4000);

        // Other
        builder.Property(e => e.Remark).HasMaxLength(4000);
    }
}
