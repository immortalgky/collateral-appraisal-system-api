namespace Appraisal.Infrastructure.Configurations;

public class ValuationAnalysisConfiguration : IEntityTypeConfiguration<ValuationAnalysis>
{
    public void Configure(EntityTypeBuilder<ValuationAnalysis> builder)
    {
        builder.ToTable("ValuationAnalyses");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(v => v.AppraisalId).IsRequired();
        builder.Property(v => v.ValuationApproach).IsRequired().HasMaxLength(50);
        builder.Property(v => v.ValuationDate).IsRequired();

        builder.Property(v => v.AppraisedValue).HasPrecision(18, 2);
        builder.Property(v => v.ForcedSaleValue).HasPrecision(18, 2);
        builder.Property(v => v.InsuranceValue).HasPrecision(18, 2);
        builder.Property(v => v.Currency).HasMaxLength(3);

        builder.Property(v => v.AppraiserOpinion).HasMaxLength(4000);
        builder.Property(v => v.ValuationNotes).HasMaxLength(4000);

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.CreatedBy).IsRequired();

        builder.HasIndex(v => v.AppraisalId).IsUnique();
    }
}