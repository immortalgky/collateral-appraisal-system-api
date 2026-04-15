using Parameter.PricingTemplates.Models;

namespace Parameter.PricingTemplates.Configurations;

public class PricingTemplateCategoryConfiguration : IEntityTypeConfiguration<PricingTemplateCategory>
{
    public void Configure(EntityTypeBuilder<PricingTemplateCategory> builder)
    {
        builder.ToTable("PricingTemplateCategories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.PricingTemplateSectionId).IsRequired();
        builder.Property(c => c.CategoryType).IsRequired().HasMaxLength(50);
        builder.Property(c => c.CategoryName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Identifier).IsRequired().HasMaxLength(50);
        builder.Property(c => c.DisplaySeq).IsRequired();

        builder.HasMany(c => c.Assumptions)
            .WithOne()
            .HasForeignKey(a => a.PricingTemplateCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
