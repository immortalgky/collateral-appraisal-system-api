namespace Appraisal.Infrastructure.Configurations;

public class MarketComparableTemplateConfiguration : IEntityTypeConfiguration<MarketComparableTemplate>
{
    public void Configure(EntityTypeBuilder<MarketComparableTemplate> builder)
    {
        builder.ToTable("MarketComparableTemplates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.TemplateCode).IsRequired().HasMaxLength(50);
        builder.Property(t => t.TemplateName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.PropertyType).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(t => t.CreatedOn).IsRequired();
        builder.Property(t => t.CreatedBy).IsRequired();

        builder.HasIndex(t => t.TemplateCode).IsUnique();
        builder.HasIndex(t => t.PropertyType);
        builder.HasIndex(t => t.IsActive);

        // Navigation to template factors
        builder.HasMany(t => t.TemplateFactors)
            .WithOne()
            .HasForeignKey(tf => tf.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.TemplateFactors).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class MarketComparableTemplateFactorConfiguration : IEntityTypeConfiguration<MarketComparableTemplateFactor>
{
    public void Configure(EntityTypeBuilder<MarketComparableTemplateFactor> builder)
    {
        builder.ToTable("MarketComparableTemplateFactors");

        builder.HasKey(tf => tf.Id);
        builder.Property(tf => tf.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(tf => tf.TemplateId).IsRequired();
        builder.Property(tf => tf.FactorId).IsRequired();
        builder.Property(tf => tf.DisplaySequence).IsRequired();
        builder.Property(tf => tf.IsMandatory).IsRequired().HasDefaultValue(false);

        builder.Property(tf => tf.CreatedOn).IsRequired();
        builder.Property(tf => tf.CreatedBy).IsRequired();

        builder.HasIndex(tf => new { tf.TemplateId, tf.FactorId }).IsUnique();
        builder.HasIndex(tf => tf.TemplateId);
        builder.HasIndex(tf => tf.FactorId);

        // Navigation to Factor for eager loading
        builder.HasOne(tf => tf.Factor)
            .WithMany()
            .HasForeignKey(tf => tf.FactorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
