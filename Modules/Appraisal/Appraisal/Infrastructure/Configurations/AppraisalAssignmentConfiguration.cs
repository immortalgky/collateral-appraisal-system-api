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

        // AssignmentMode Value Object (stored as string)
        builder.OwnsOne(a => a.AssignmentMode, am =>
        {
            am.Property(m => m.Code)
                .HasColumnName("AssignmentMode")
                .IsRequired()
                .HasMaxLength(30);
        });

        // AssignmentStatus Value Object (stored as string)
        builder.OwnsOne(a => a.AssignmentStatus, s =>
        {
            s.Property(st => st.Code)
                .HasColumnName("AssignmentStatus")
                .IsRequired()
                .HasMaxLength(30);
        });

        // Assignee
        builder.Property(a => a.AssigneeUserId);
        builder.Property(a => a.AssigneeCompanyId);

        // External Appraiser Details
        builder.Property(a => a.ExternalAppraiserId);
        builder.Property(a => a.ExternalAppraiserName)
            .HasMaxLength(200);
        builder.Property(a => a.ExternalAppraiserLicense)
            .HasMaxLength(50);

        // Assignment Source
        builder.Property(a => a.AssignmentSource)
            .IsRequired()
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
            .IsRequired();
        builder.Property(a => a.StartedAt);
        builder.Property(a => a.CompletedAt);
        builder.Property(a => a.RejectionReason)
            .HasMaxLength(500);
        builder.Property(a => a.CancellationReason)
            .HasMaxLength(500);

        // Audit Fields
        builder.Property(a => a.CreatedOn)
            .IsRequired();
        builder.Property(a => a.CreatedBy)
            .IsRequired();
        builder.Property(a => a.UpdatedOn);
        builder.Property(a => a.UpdatedBy);

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