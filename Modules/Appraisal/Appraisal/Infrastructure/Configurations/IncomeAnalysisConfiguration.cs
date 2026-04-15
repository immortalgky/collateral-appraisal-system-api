using Appraisal.Domain.Appraisals.Income;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>EF Core configuration for IncomeAnalysis — 1:1 child of PricingAnalysisMethod.</summary>
public class IncomeAnalysisConfiguration : IEntityTypeConfiguration<IncomeAnalysis>
{
    public void Configure(EntityTypeBuilder<IncomeAnalysis> builder)
    {
        builder.ToTable("IncomeAnalyses");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.PricingAnalysisMethodId).IsRequired();
        builder.HasIndex(a => a.PricingAnalysisMethodId).IsUnique();

        builder.Property(a => a.TemplateCode).IsRequired().HasMaxLength(100);
        builder.Property(a => a.TemplateName).IsRequired().HasMaxLength(200);

        builder.Property(a => a.TotalNumberOfYears).IsRequired();
        builder.Property(a => a.TotalNumberOfDayInYear).IsRequired().HasDefaultValue(365);

        builder.Property(a => a.CapitalizeRate).HasPrecision(5, 2);
        builder.Property(a => a.DiscountedRate).HasPrecision(5, 2);

        builder.Property(a => a.FinalValue).HasPrecision(18, 2);
        builder.Property(a => a.FinalValueRounded).HasPrecision(18, 2);

        // Owned IncomeSummary (all JSON columns)
        builder.OwnsOne(a => a.Summary, s =>
        {
            s.Property(x => x.ContractRentalFeeJson)
                .HasColumnName("Summary_ContractRentalFeeJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");

            s.Property(x => x.GrossRevenueJson)
                .HasColumnName("Summary_GrossRevenueJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");

            s.Property(x => x.GrossRevenueProportionalJson)
                .HasColumnName("Summary_GrossRevenueProportionalJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");

            s.Property(x => x.TerminalRevenueJson)
                .HasColumnName("Summary_TerminalRevenueJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");

            s.Property(x => x.TotalNetJson)
                .HasColumnName("Summary_TotalNetJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");

            s.Property(x => x.DiscountJson)
                .HasColumnName("Summary_DiscountJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");

            s.Property(x => x.PresentValueJson)
                .HasColumnName("Summary_PresentValueJson")
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("[]");
        });

        // Child sections with cascade delete
        builder.HasMany(a => a.Sections)
            .WithOne()
            .HasForeignKey(s => s.IncomeAnalysisId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
