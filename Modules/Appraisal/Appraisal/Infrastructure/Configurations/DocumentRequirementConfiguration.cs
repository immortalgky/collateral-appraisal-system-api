namespace Appraisal.Infrastructure.Configurations;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("DocumentTypes");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Description)
            .HasMaxLength(500);

        builder.Property(d => d.Category)
            .HasMaxLength(100);

        builder.Property(d => d.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.CreatedOn).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();

        // Unique constraint on Code
        builder.HasIndex(d => d.Code).IsUnique();

        // Navigation to requirements
        builder.HasMany(d => d.Requirements)
            .WithOne(r => r.DocumentType)
            .HasForeignKey(r => r.DocumentTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class DocumentRequirementConfiguration : IEntityTypeConfiguration<DocumentRequirement>
{
    public void Configure(EntityTypeBuilder<DocumentRequirement> builder)
    {
        builder.ToTable("DocumentRequirements");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.DocumentTypeId)
            .IsRequired();

        // CollateralTypeCode is nullable (NULL = application-level)
        builder.Property(r => r.CollateralTypeCode)
            .HasMaxLength(10);

        builder.Property(r => r.IsRequired)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.Notes)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedOn).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();

        // Unique constraint on (DocumentTypeId, CollateralTypeCode)
        // Note: SQL Server handles NULL values in unique constraints properly
        builder.HasIndex(r => new { r.DocumentTypeId, r.CollateralTypeCode })
            .IsUnique()
            .HasFilter(null); // Include NULLs in unique constraint

        // Indexes for query performance
        builder.HasIndex(r => r.CollateralTypeCode);
        builder.HasIndex(r => r.IsActive);
    }
}
