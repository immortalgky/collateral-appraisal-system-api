namespace Appraisal.Infrastructure.Configurations;

public class AppraisalReviewConfiguration : IEntityTypeConfiguration<AppraisalReview>
{
    public void Configure(EntityTypeBuilder<AppraisalReview> builder)
    {
        builder.ToTable("AppraisalReviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.AppraisalId).IsRequired();
        builder.Property(r => r.ReviewLevel).IsRequired().HasMaxLength(50);
        builder.Property(r => r.ReviewSequence).IsRequired();

        builder.OwnsOne(r => r.Status,
            s => { s.Property(st => st.Code).HasColumnName("Status").IsRequired().HasMaxLength(30); });

        builder.Property(r => r.TeamName).HasMaxLength(200);
        builder.Property(r => r.MeetingReference).HasMaxLength(100);
        builder.Property(r => r.ReviewComments).HasMaxLength(2000);
        builder.Property(r => r.ReturnReason).HasMaxLength(500);

        builder.Property(r => r.CreatedOn).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();

        builder.HasIndex(r => r.AppraisalId);
        builder.HasIndex(r => r.AssignedTo);
        builder.HasIndex(r => r.TeamId);
        builder.HasIndex(r => r.CommitteeId);
    }
}