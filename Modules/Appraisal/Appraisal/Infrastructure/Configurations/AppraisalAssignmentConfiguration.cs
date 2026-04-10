namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the AppraisalAssignment entity.
/// </summary>
public class AppraisalAssignmentConfiguration : IEntityTypeConfiguration<AppraisalAssignment>
{
    public void Configure(EntityTypeBuilder<AppraisalAssignment> builder)
    {
        builder.ToTable("AppraisalAssignments");

        // Primary Key
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Core Properties
        builder.Property(a => a.AppraisalId)
            .IsRequired();

        // AssignmentType Value Object (stored as string)
        builder.Property(a => a.AssignmentType)
            .HasConversion(
                v => v.Code,
                v => AssignmentType.FromString(v))
            .HasColumnName("AssignmentType")
            .IsRequired()
            .HasMaxLength(30);

        // AssignmentStatus Value Object (stored as string)
        // builder.OwnsOne(a => a.AssignmentStatus, s =>
        // {
        //     s.Property(st => st.Code)
        //         .HasColumnName("AssignmentStatus")
        //         .IsRequired()
        //         .HasMaxLength(30);
        // });
        builder.Property(a => a.AssignmentStatus)
            .HasConversion(
                v => v.Code,
                v => AssignmentStatus.FromString(v))
            .HasColumnName("AssignmentStatus")
            .IsRequired()
            .HasMaxLength(30);

        // Assignee
        builder.Property(a => a.AssigneeUserId).HasMaxLength(100);
        builder.Property(a => a.AssigneeCompanyId).HasMaxLength(100);

        // External Appraiser Details
        builder.Property(a => a.ExternalAppraiserId).HasMaxLength(100);
        builder.Property(a => a.ExternalAppraiserName)
            .HasMaxLength(200);

        // Internal Appraiser Details
        builder.Property(a => a.InternalAppraiserId).HasMaxLength(100);
        builder.Property(a => a.InternalAppraiserName)
            .HasMaxLength(200);

        // Assignment Method
        builder.Property(a => a.AssignmentMethod)
            .HasConversion(v => v.Code, v => AssignmentMethod.FromString(v))
            .IsRequired()
            .HasMaxLength(30);
        builder.Property(a => a.InternalFollowupAssignmentMethod)
            .HasConversion(
                v => v != null ? v.Code : null,
                v => v != null ? AssignmentMethod.FromString(v) : null)
            .HasMaxLength(30);
        builder.Property(a => a.AutoRuleId);
        builder.Property(a => a.QuotationRequestId);

        // Reassignment Chain
        builder.Property(a => a.PreviousAssignmentId);
        builder.Property(a => a.ReassignmentNumber)
            .IsRequired()
            .HasDefaultValue(1);

        // Progress Tracking
        builder.Property(a => a.ProgressPercent)
            .IsRequired()
            .HasDefaultValue(0);
        builder.Property(a => a.LastProgressUpdate);

        // Timestamps
        builder.Property(a => a.AssignedAt)
            .IsRequired();
        builder.Property(a => a.AssignedBy)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(a => a.StartedAt);
        builder.Property(a => a.CompletedAt);
        builder.Property(a => a.RejectionReason)
            .HasMaxLength(500);
        builder.Property(a => a.CancellationReason)
            .HasMaxLength(500);
        builder.Property(a => a.Notes)
            .HasMaxLength(4000);

        // Self-referencing FK for reassignment chain
        builder.HasOne<AppraisalAssignment>()
            .WithMany()
            .HasForeignKey(a => a.PreviousAssignmentId)
            .OnDelete(DeleteBehavior.NoAction);

        // Indexes
        builder.HasIndex(a => a.AppraisalId);
        builder.HasIndex(a => a.AssigneeUserId);
        builder.HasIndex(a => a.AssigneeCompanyId);
        builder.HasIndex(a => a.PreviousAssignmentId);
    }
}