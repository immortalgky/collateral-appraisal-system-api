using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class CondoModelConfiguration : IEntityTypeConfiguration<CondoModel>
{
    public void Configure(EntityTypeBuilder<CondoModel> builder)
    {
        builder.ToTable("CondoModels");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId);

        // Model Info
        builder.Property(e => e.ModelName).HasMaxLength(200);
        builder.Property(e => e.ModelDescription).HasMaxLength(500);
        builder.Property(e => e.BuildingNumber).HasMaxLength(50);

        // Pricing
        builder.Property(e => e.StartingPriceMin).HasPrecision(18, 2);
        builder.Property(e => e.StartingPriceMax).HasPrecision(18, 2);

        // Usable Area
        builder.Property(e => e.UsableAreaMin).HasPrecision(10, 2);
        builder.Property(e => e.UsableAreaMax).HasPrecision(10, 2);
        builder.Property(e => e.StandardUsableArea).HasPrecision(10, 2);

        // Insurance
        builder.Property(e => e.FireInsuranceCondition).HasMaxLength(200);

        // Layout
        builder.Property(e => e.RoomLayoutType).HasMaxLength(100);
        builder.Property(e => e.RoomLayoutTypeOther).HasMaxLength(200);

        // Materials
        builder.Property(e => e.GroundFloorMaterialType).HasMaxLength(100);
        builder.Property(e => e.GroundFloorMaterialTypeOther).HasMaxLength(200);
        builder.Property(e => e.UpperFloorMaterialType).HasMaxLength(100);
        builder.Property(e => e.UpperFloorMaterialTypeOther).HasMaxLength(200);
        builder.Property(e => e.BathroomFloorMaterialType).HasMaxLength(100);
        builder.Property(e => e.BathroomFloorMaterialTypeOther).HasMaxLength(200);

        // JSON columns
        builder.Property(e => e.ImageDocumentIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(2000)");

        // Area Details (owned collection)
        builder.OwnsMany(e => e.AreaDetails, areaDetail =>
        {
            areaDetail.ToTable("CondoModelAreaDetails");
            areaDetail.WithOwner().HasForeignKey("CondoModelId");
            areaDetail.HasKey("Id");
            areaDetail.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            areaDetail.Property(p => p.AreaDescription).HasMaxLength(200).HasColumnName("AreaDescription");
            areaDetail.Property(p => p.AreaSize).HasPrecision(10, 2).HasColumnName("AreaSize");
        });

        // Backing field for owned collection
        builder.Navigation(e => e.AreaDetails)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Other
        builder.Property(e => e.Remark).HasMaxLength(500);
    }
}
