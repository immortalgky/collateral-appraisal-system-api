namespace Appraisal.Infrastructure.Configurations;

public class AppraisalSuspensionLogConfiguration : IEntityTypeConfiguration<AppraisalSuspensionLog>
{
    public void Configure(EntityTypeBuilder<AppraisalSuspensionLog> builder)
    {
        builder.ToTable("AppraisalSuspensionLogs");

        builder.HasKey(a => a.Id);
        // Assigned in the constructor (Guid.CreateVersion7).
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AppraisalId).IsRequired();
        // "Suspended" | "Reactivated"
        builder.Property(a => a.Action).IsRequired().HasMaxLength(30);
        // Same 4000 limit as SuspendAppraisalCommandValidator.
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(4000);
        builder.Property(a => a.ActionBy).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ActionAt).IsRequired();

        // History per appraisal, newest first.
        builder.HasIndex(a => new { a.AppraisalId, a.ActionAt })
            .HasDatabaseName("IX_AppraisalSuspensionLogs_Appraisal_ActionAt");

        // No FK to Appraisals on purpose: the audit trail outlives the row (same as AppraisalPropertyCorrectionLog).
    }
}
