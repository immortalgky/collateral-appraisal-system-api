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

        // Null = generic ladder (any appraisal type). Matches Appraisals.AppraisalType's length.
        builder.Property(f => f.AppraisalType)
            .HasMaxLength(50);

        builder.Property(f => f.BaseAmount)
            .HasPrecision(18, 2);

        builder.Property(f => f.MinSellingPrice)
            .HasPrecision(18, 2);

        builder.Property(f => f.MaxSellingPrice)
            .HasPrecision(18, 2);

        builder.Property(f => f.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Composite unique: same FeeCode can have multiple tiers distinguished by MinSellingPrice,
        // and a separate ladder per AppraisalType. SQL Server treats NULLs as equal in a unique
        // index, so the generic (null-type) ladder still allows only one row per FeeCode+Min.
        // HasFilter(null) suppresses EF's default "[AppraisalType] IS NOT NULL" filter for nullable
        // key columns — without it the generic ladder would lose its uniqueness guarantee entirely.
        builder.HasIndex(f => new { f.FeeCode, f.AppraisalType, f.MinSellingPrice })
            .IsUnique()
            .HasFilter(null);

        // Seed data — Appraisal Fee (01) has 3 generic selling-price tiers plus a flat
        // PreAppraisal (block / M-F project) tier. Fee names resolve from the TypeOfFee
        // parameter group by code, so only the code is stored here.
        builder.HasData(
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                FeeCode = "01",
                AppraisalType = (string?)null,
                BaseAmount = 2_500m,
                MinSellingPrice = 0m,
                MaxSellingPrice = (decimal?)7_000_000m,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000004"),
                FeeCode = "01",
                AppraisalType = (string?)null,
                BaseAmount = 3_000m,
                MinSellingPrice = 7_000_001m,
                MaxSellingPrice = (decimal?)10_000_000m,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000005"),
                FeeCode = "01",
                AppraisalType = (string?)null,
                BaseAmount = 3_500m,
                MinSellingPrice = 10_000_001m,
                MaxSellingPrice = (decimal?)null,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            },
            // Block / M-F project deals are charged a flat 10,000 (ex-VAT) regardless of selling
            // price, so this ladder is a single open-ended band.
            // NOTE: block *reappraisals* (Purpose "09") are stamped AppraisalType "ReAppraisal",
            // not "PreAppraisal" (AppraisalCreationService gives ReAppraisal precedence over the
            // block check), so they deliberately stay on the generic ladder for now.
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000006"),
                FeeCode = "01",
                AppraisalType = (string?)AppraisalTypes.PreAppraisal,
                BaseAmount = 10_000m,
                MinSellingPrice = 0m,
                MaxSellingPrice = (decimal?)null,
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            }
        );
    }
}
