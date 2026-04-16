using Appraisal.Domain.Appraisals.Income;

namespace Appraisal.Infrastructure.Configurations;

/// <summary>EF Core configuration for IncomeSection — child of IncomeAnalysis.</summary>
public class IncomeSectionConfiguration : IEntityTypeConfiguration<IncomeSection>
{
    public void Configure(EntityTypeBuilder<IncomeSection> builder)
    {
        builder.ToTable("IncomeSections");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.IncomeAnalysisId).IsRequired();
        builder.Property(s => s.SectionType).IsRequired().HasMaxLength(50);
        builder.Property(s => s.SectionName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Identifier).IsRequired().HasMaxLength(20);
        builder.Property(s => s.DisplaySeq).IsRequired();
        builder.Property(s => s.TotalSectionValuesJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");

        // Child categories with cascade delete
        builder.HasMany(s => s.Categories)
            .WithOne()
            .HasForeignKey(c => c.IncomeSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .Metadata.PrincipalToDependent!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.IncomeAnalysisId);
    }
}
