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

        // Address lists and attachment refs are small and always read with the email: JSON columns.
        builder.PrimitiveCollection(e => e.ExtraTo);
        builder.PrimitiveCollection(e => e.Cc);
        builder.OwnsMany(e => e.Attachments, a => a.ToJson());

        // Read per appraisal (the slip's history); a case has only a few rows, so the send's assignment filter needs no index of its own.
        builder.HasIndex(e => e.AppraisalId);
    }
}
