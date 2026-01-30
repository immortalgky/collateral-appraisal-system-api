using Appraisal.Domain.ComparativeAnalysis;

namespace Appraisal.Infrastructure.Configurations;

public class ComparativeAnalysisTemplateConfiguration : IEntityTypeConfiguration<ComparativeAnalysisTemplate>
{
    public void Configure(EntityTypeBuilder<ComparativeAnalysisTemplate> builder)
    {
        builder.ToTable("ComparativeAnalysisTemplates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.TemplateCode)
            .IsRequired()
            .HasMaxLength(50);
        builder.HasIndex(t => t.TemplateCode).IsUnique();

        builder.Property(t => t.TemplateName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.PropertyType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasMany(t => t.Factors)
            .WithOne()
            .HasForeignKey(f => f.TemplateId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => t.PropertyType);
    }
}

public class ComparativeAnalysisTemplateFactorConfiguration : IEntityTypeConfiguration<ComparativeAnalysisTemplateFactor>
{
    public void Configure(EntityTypeBuilder<ComparativeAnalysisTemplateFactor> builder)
    {
        builder.ToTable("ComparativeAnalysisTemplateFactors");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.TemplateId).IsRequired();
        builder.Property(f => f.FactorId).IsRequired();
        builder.Property(f => f.DisplaySequence).IsRequired();
        builder.Property(f => f.IsMandatory).IsRequired().HasDefaultValue(false);
        builder.Property(f => f.DefaultWeight).HasPrecision(5, 2);

        builder.HasIndex(f => new { f.TemplateId, f.FactorId }).IsUnique();
        builder.HasIndex(f => f.TemplateId);
    }
}
