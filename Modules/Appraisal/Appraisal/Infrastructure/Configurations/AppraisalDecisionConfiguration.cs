namespace Appraisal.Infrastructure.Configurations;

public class AppraisalDecisionConfiguration : IEntityTypeConfiguration<AppraisalDecision>
{
    public void Configure(EntityTypeBuilder<AppraisalDecision> builder)
    {
        builder.ToTable("AppraisalDecisions");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.AppraisalId).IsRequired();
        builder.HasIndex(d => d.AppraisalId).IsUnique();

        // Parameter Code fields
        builder.Property(d => d.ConditionType).HasMaxLength(100);
        builder.Property(d => d.RemarkType).HasMaxLength(100);
        builder.Property(d => d.AppraiserOpinionType).HasMaxLength(100);
        builder.Property(d => d.CommitteeOpinionType).HasMaxLength(100);

        // Free text fields
        builder.Property(d => d.Condition).HasMaxLength(2000);
        builder.Property(d => d.Remark).HasMaxLength(2000);
        builder.Property(d => d.AppraiserOpinion).HasMaxLength(2000);
        builder.Property(d => d.CommitteeOpinion).HasMaxLength(2000);
        builder.Property(d => d.AdditionalAssumptions).HasMaxLength(4000);

        // Decimal
        builder.Property(d => d.TotalAppraisalPriceReview).HasColumnType("decimal(18,2)");

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();
    }
}
