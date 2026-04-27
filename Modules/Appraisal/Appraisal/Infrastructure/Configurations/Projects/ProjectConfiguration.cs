using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        // Primary Key — no server default; Guid.CreateVersion7() set in domain factory
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // 1:1 with Appraisal (FK on Project side) — cascade from Appraisal to Project
        builder.Property(p => p.AppraisalId).IsRequired();
        builder.HasIndex(p => p.AppraisalId).IsUnique();
        builder.HasOne<Appraisal.Domain.Appraisals.Appraisal>()
            .WithOne()
            .HasForeignKey<Project>(p => p.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProjectType discriminator stored as int
        builder.Property(p => p.ProjectType)
            .HasConversion<int>()
            .IsRequired();

        // Project Info
        builder.Property(p => p.ProjectName).HasMaxLength(500);
        builder.Property(p => p.ProjectDescription).HasMaxLength(500);
        builder.Property(p => p.Developer).HasMaxLength(200);
        builder.Property(p => p.LandOffice).HasMaxLength(200);

        // Land Area
        builder.Property(p => p.LandAreaRai).HasPrecision(10, 4);
        builder.Property(p => p.LandAreaNgan).HasPrecision(10, 4);
        builder.Property(p => p.LandAreaWa).HasPrecision(10, 4);

        // Location
        builder.Property(p => p.Postcode).HasMaxLength(20);
        builder.Property(p => p.LocationNumber).HasMaxLength(200);
        builder.Property(p => p.Road).HasMaxLength(200);
        builder.Property(p => p.Soi).HasMaxLength(200);

        // GPS Coordinates (Value Object)
        builder.OwnsOne(p => p.Coordinates, coord =>
        {
            coord.Property(c => c.Latitude).HasColumnName("Latitude").HasPrecision(10, 7);
            coord.Property(c => c.Longitude).HasColumnName("Longitude").HasPrecision(10, 7);
        });

        // Administrative Address (Value Object)
        builder.OwnsOne(p => p.Address, addr =>
        {
            addr.Property(a => a.SubDistrict).HasColumnName("SubDistrict").HasMaxLength(100);
            addr.Property(a => a.District).HasColumnName("District").HasMaxLength(100);
            addr.Property(a => a.Province).HasColumnName("Province").HasMaxLength(100);
            addr.Property(a => a.LandOffice).HasColumnName("AddressLandOffice").HasMaxLength(200);
        });

        // JSON columns
        builder.Property(p => p.Utilities)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(1000)");

        builder.Property(p => p.Facilities)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(1000)");

        builder.Property(p => p.UtilitiesOther).HasMaxLength(500);
        builder.Property(p => p.FacilitiesOther).HasMaxLength(500);

        // Type-specific nullable fields
        builder.Property(p => p.BuiltOnTitleDeedNumber).HasMaxLength(100);

        // Other
        builder.Property(p => p.Remark).HasMaxLength(4000);

        // Child relationships (child entities are their own tables — NOT owned)
        builder.HasMany(p => p.Towers)
            .WithOne()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Models)
            .WithOne()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Units)
            .WithOne()
            .HasForeignKey(u => u.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.UnitUploads)
            .WithOne()
            .HasForeignKey(u => u.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.PricingAssumption)
            .WithOne()
            .HasForeignKey<ProjectPricingAssumption>(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Land)
            .WithOne()
            .HasForeignKey<ProjectLand>(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backing fields for private collections
        builder.Navigation(p => p.Towers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Models).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Units).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.UnitUploads).UsePropertyAccessMode(PropertyAccessMode.Field);
        // UnitPrices hang off ProjectUnit (1:1). Access via dbContext.ProjectUnitPrices — not a Project navigation.
    }
}
