namespace Appraisal.Infrastructure.Configurations;

public class AppraisalReviewConfiguration : IEntityTypeConfiguration<AppraisalReview>
{
    public void Configure(EntityTypeBuilder<AppraisalReview> builder)
    {
        builder.ToTable("AppraisalReviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.AppraisalId).IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();

        // One committee-approval outcome row per appraisal.
        builder.HasIndex(r => r.AppraisalId).IsUnique();
        builder.HasIndex(r => r.CommitteeId);
        builder.HasIndex(r => r.MeetingId);
    }
}
