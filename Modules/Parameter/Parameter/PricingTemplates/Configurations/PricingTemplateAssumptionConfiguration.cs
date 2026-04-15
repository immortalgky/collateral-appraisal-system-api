using Parameter.PricingTemplates.Models;

namespace Parameter.PricingTemplates.Configurations;

public class PricingTemplateAssumptionConfiguration : IEntityTypeConfiguration<PricingTemplateAssumption>
{
    public void Configure(EntityTypeBuilder<PricingTemplateAssumption> builder)
    {
        builder.ToTable("PricingTemplateAssumptions");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.PricingTemplateCategoryId).IsRequired();
        builder.Property(a => a.AssumptionType).IsRequired().HasMaxLength(10);
        builder.Property(a => a.AssumptionName).HasMaxLength(200);
        builder.Property(a => a.Identifier).IsRequired().HasMaxLength(50);
        builder.Property(a => a.DisplaySeq).IsRequired();
        builder.Property(a => a.MethodTypeCode).IsRequired().HasMaxLength(5);
        builder.Property(a => a.MethodDetailJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}");
    }
}
