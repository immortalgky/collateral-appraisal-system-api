using Parameter.PricingTemplates.Models;

namespace Parameter.PricingTemplates.Configurations;

public class PricingTemplateConfiguration : IEntityTypeConfiguration<PricingTemplate>
{
    public void Configure(EntityTypeBuilder<PricingTemplate> builder)
    {
        builder.ToTable("PricingTemplates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Code).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.TemplateType).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.TotalNumberOfYears).IsRequired();
        builder.Property(t => t.TotalNumberOfDayInYear).IsRequired().HasDefaultValue(365);
        builder.Property(t => t.CapitalizeRate).IsRequired().HasPrecision(5, 2);
        builder.Property(t => t.DiscountedRate).IsRequired().HasPrecision(5, 2);
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.DisplaySeq).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();

        builder.HasMany(t => t.Sections)
            .WithOne()
            .HasForeignKey(s => s.PricingTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
