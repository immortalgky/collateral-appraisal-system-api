namespace Appraisal.Infrastructure.Configurations;

public class AppraisalPropertyCorrectionLogConfiguration
    : IEntityTypeConfiguration<AppraisalPropertyCorrectionLog>
{
    public void Configure(EntityTypeBuilder<AppraisalPropertyCorrectionLog> builder)
    {
        builder.ToTable("AppraisalPropertyCorrectionLogs");

        builder.HasKey(a => a.Id);
        // Ids are assigned in the constructor (Guid.CreateVersion7) so the database must not.
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AppraisalId).IsRequired();
        builder.Property(a => a.AppraisalPropertyId).IsRequired();
        builder.Property(a => a.PropertyType).IsRequired().HasMaxLength(50);
        builder.Property(a => a.ChangedFields).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(4000);
        builder.Property(a => a.ChangedBy).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ChangedAt).IsRequired();

        // Drives the per-appraisal history panel.
        builder.HasIndex(a => new { a.AppraisalId, a.ChangedAt })
            .HasDatabaseName("IX_AppraisalPropertyCorrectionLogs_Appraisal_ChangedAt");

        builder.HasIndex(a => a.AppraisalPropertyId)
            .HasDatabaseName("IX_AppraisalPropertyCorrectionLogs_Property");

        // No FK to Appraisals/AppraisalProperties on purpose: the audit trail must survive
        // independently of the rows it describes (same choice as CollateralMasterAuditLog).
    }
}
