namespace Appraisal.Infrastructure.Configurations;

public class ExternalEngagementCycleConfiguration : IEntityTypeConfiguration<ExternalEngagementCycle>
{
    public void Configure(EntityTypeBuilder<ExternalEngagementCycle> builder)
    {
        builder.ToTable("ExternalEngagementCycles");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever(); // App supplies Guid.CreateVersion7() — no server-side generation.

        builder.Property(c => c.AppraisalAssignmentId)
            .IsRequired();

        builder.Property(c => c.CycleNumber)
            .IsRequired();

        builder.Property(c => c.OpenedAt)
            .IsRequired();

        builder.Property(c => c.ClosedAt);

        builder.Property(c => c.BusinessMinutes);

        // CycleStatus stored as nvarchar — same column name and values as before (no migration needed).
        builder.Property(c => c.Status)
            .HasConversion(v => v.Code, v => CycleStatus.FromString(v))
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(c => new { c.AppraisalAssignmentId, c.CycleNumber })
            .IsUnique()
            .HasDatabaseName("IX_ExternalEngagementCycles_AssignmentId_CycleNumber");
    }
}