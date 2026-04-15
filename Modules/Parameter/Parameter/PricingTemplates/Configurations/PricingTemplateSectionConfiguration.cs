using Parameter.PricingTemplates.Models;

namespace Parameter.PricingTemplates.Configurations;

public class PricingTemplateSectionConfiguration : IEntityTypeConfiguration<PricingTemplateSection>
{
    public void Configure(EntityTypeBuilder<PricingTemplateSection> builder)
    {
        builder.ToTable("PricingTemplateSections");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.PricingTemplateId).IsRequired();
        builder.Property(s => s.SectionType).IsRequired().HasMaxLength(50);
        builder.Property(s => s.SectionName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Identifier).IsRequired().HasMaxLength(50);
        builder.Property(s => s.DisplaySeq).IsRequired();

        builder.HasMany(s => s.Categories)
            .WithOne()
            .HasForeignKey(c => c.PricingTemplateSectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
