namespace Appraisal.Infrastructure.Configurations;

public class AssignmentEmailConfiguration : IEntityTypeConfiguration<AssignmentEmail>
{
    public void Configure(EntityTypeBuilder<AssignmentEmail> builder)
    {
        builder.ToTable("AssignmentEmails");
        builder.HasKey(e => e.Id);
        // Ids are assigned in the factories (Guid.CreateVersion7) so the database must not.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        // Empty on a NotSent row: only the admin's choice is recorded.
        builder.Property(e => e.BodyHtml).IsRequired();
        // Notification cuts the reason to 200 (AssignmentEmailFailureNotifier.MaxReasonLength).
        builder.Property(e => e.FailureReason).HasMaxLength(200);

        // Address lists and attachment refs are small and always read with the email: JSON columns.
        builder.PrimitiveCollection(e => e.ExtraTo);
        builder.PrimitiveCollection(e => e.Cc);
        builder.OwnsMany(e => e.Attachments, a => a.ToJson());

        // Read per appraisal (the slip's history) and per assignment (the send).
        builder.HasIndex(e => e.AppraisalId);
        builder.HasIndex(e => e.AssignmentId);
    }
}
