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
        builder.Property(d => d.ExternalAppraiserOpinionType).HasMaxLength(100);
        builder.Property(d => d.CommitteeOpinionType).HasMaxLength(100);
        builder.Property(d => d.InternalAppraiserOpinionType).HasMaxLength(100);

        // Free text fields
        builder.Property(d => d.Condition);
        builder.Property(d => d.Remark);
        builder.Property(d => d.CommitteeOpinion);
        builder.Property(d => d.ExternalAppraiserOpinion);
        builder.Property(d => d.InternalAppraiserOpinion);
        builder.Property(d => d.AdditionalAssumptions);

        // Decimal
        builder.Property(d => d.TotalAppraisalPriceReview).HasColumnType("decimal(18,2)");

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();
    }
}
