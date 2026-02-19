namespace Appraisal.Infrastructure.Configurations;

public class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
    public void Configure(EntityTypeBuilder<FeeStructure> builder)
    {
        builder.ToTable("FeeStructures");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.FeeCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(f => f.FeeName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.BaseAmount)
            .HasPrecision(18, 2);

        builder.Property(f => f.MinSellingPrice)
            .HasPrecision(18, 2);

        builder.Property(f => f.MaxSellingPrice)
            .HasPrecision(18, 2);

        builder.Property(f => f.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Composite unique: same FeeCode can have multiple tiers distinguished by MinSellingPrice
        builder.HasIndex(f => new { f.FeeCode, f.MinSellingPrice })
            .IsUnique();

        // Seed data — Appraisal Fee (01) has 3 price tiers; Travel (02) and Urgent (03) are flat
        builder.HasData(
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                FeeCode = "01",
                FeeName = "Appraisal Fee",
                BaseAmount = 3_500m,
                MinSellingPrice = 0m,
                MaxSellingPrice = (decimal?)5_000_000m,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000004"),
                FeeCode = "01",
                FeeName = "Appraisal Fee",
                BaseAmount = 5_000m,
                MinSellingPrice = 5_000_001m,
                MaxSellingPrice = (decimal?)10_000_000m,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000005"),
                FeeCode = "01",
                FeeName = "Appraisal Fee",
                BaseAmount = 7_000m,
                MinSellingPrice = 10_000_001m,
                MaxSellingPrice = (decimal?)null,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                FeeCode = "02",
                FeeName = "Travel Fee",
                BaseAmount = 0m,
                MinSellingPrice = 0m,
                MaxSellingPrice = (decimal?)null,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                FeeCode = "03",
                FeeName = "Urgent Fee",
                BaseAmount = 0m,
                MinSellingPrice = 0m,
                MaxSellingPrice = (decimal?)null,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            }
        );
    }
}
