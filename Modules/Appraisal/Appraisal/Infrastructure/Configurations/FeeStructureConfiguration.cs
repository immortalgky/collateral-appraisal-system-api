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

        builder.Property(f => f.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(f => f.FeeCode)
            .IsUnique();

        // Seed data
        builder.HasData(
            new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                FeeCode = "01",
                FeeName = "Appraisal Fee",
                BaseAmount = 0m,
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
                IsActive = true,
                CreatedOn = (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = (string?)"System"
            }
        );
    }
}
