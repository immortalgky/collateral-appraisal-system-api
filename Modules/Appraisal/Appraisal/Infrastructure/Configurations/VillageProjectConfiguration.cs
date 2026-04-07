using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class VillageProjectConfiguration : IEntityTypeConfiguration<VillageProject>
{
    public void Configure(EntityTypeBuilder<VillageProject> builder)
    {
        builder.ToTable("VillageProjects");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key (1:1 with Appraisal)
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId).IsUnique();

        // Project Info
        builder.Property(e => e.ProjectName).HasMaxLength(500);
        builder.Property(e => e.ProjectDescription).HasMaxLength(500);
        builder.Property(e => e.Developer).HasMaxLength(200);
        builder.Property(e => e.ProjectType).HasMaxLength(200);
        builder.Property(e => e.LandOffice).HasMaxLength(200);

        // Land Area
        builder.Property(e => e.LandAreaRai).HasPrecision(10, 4);
        builder.Property(e => e.LandAreaNgan).HasPrecision(10, 4);
        builder.Property(e => e.LandAreaWa).HasPrecision(10, 4);

        // Location
        builder.Property(e => e.Postcode).HasMaxLength(20);
        builder.Property(e => e.LocationNumber).HasMaxLength(200);
        builder.Property(e => e.Road).HasMaxLength(200);
        builder.Property(e => e.Soi).HasMaxLength(200);

        // GPS Coordinates (Value Object)
        builder.OwnsOne(e => e.Coordinates, coord =>
        {
            coord.Property(c => c.Latitude).HasColumnName("Latitude").HasPrecision(10, 7);
            coord.Property(c => c.Longitude).HasColumnName("Longitude").HasPrecision(10, 7);
        });

        // Administrative Address (Value Object)
        builder.OwnsOne(e => e.Address, addr =>
        {
            addr.Property(a => a.SubDistrict).HasColumnName("SubDistrict").HasMaxLength(100);
            addr.Property(a => a.District).HasColumnName("District").HasMaxLength(100);
            addr.Property(a => a.Province).HasColumnName("Province").HasMaxLength(100);
            addr.Property(a => a.LandOffice).HasColumnName("AddressLandOffice").HasMaxLength(200);
        });

        // JSON columns
        builder.Property(e => e.Utilities)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(1000)");

        builder.Property(e => e.Facilities)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(1000)");

        builder.Property(e => e.UtilitiesOther).HasMaxLength(500);
        builder.Property(e => e.FacilitiesOther).HasMaxLength(500);

        // Other
        builder.Property(e => e.Remark).HasMaxLength(1000);
    }
}
