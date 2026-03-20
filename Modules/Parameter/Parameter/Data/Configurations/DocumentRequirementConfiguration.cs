using Parameter.DocumentRequirements.Models;

namespace Parameter.Data.Configurations;

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

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();

        builder.HasIndex(d => d.Code).IsUnique();

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

        builder.Property(r => r.PropertyTypeCode)
            .HasMaxLength(10);

        builder.Property(r => r.PurposeCode)
            .HasMaxLength(10);

        builder.Property(r => r.IsRequired)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.Notes)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();

        builder.HasIndex(r => new { r.DocumentTypeId, r.PropertyTypeCode, r.PurposeCode })
            .IsUnique()
            .HasFilter(null);

        builder.HasIndex(r => r.PropertyTypeCode);
        builder.HasIndex(r => r.PurposeCode);
        builder.HasIndex(r => r.IsActive);
    }
}
