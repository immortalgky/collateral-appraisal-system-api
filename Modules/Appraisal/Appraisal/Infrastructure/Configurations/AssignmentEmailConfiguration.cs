namespace Appraisal.Infrastructure.Configurations;

public class AssignmentEmailConfiguration : IEntityTypeConfiguration<AssignmentEmail>
{
    public void Configure(EntityTypeBuilder<AssignmentEmail> builder)
    {
        builder.ToTable("AssignmentEmails");
        builder.HasKey(e => e.Id);
        // Ids are assigned in CreateDraft (Guid.CreateVersion7) so the database must not.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CompanyId).HasMaxLength(100);
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.BodyHtml).IsRequired();

        // Address lists and attachment refs are small and always read with the email: JSON columns.
        builder.PrimitiveCollection(e => e.ExtraTo);
        builder.PrimitiveCollection(e => e.Cc);
        builder.OwnsMany(e => e.Attachments, a => a.ToJson());

        builder.HasIndex(e => e.AppraisalId)
            .IsUnique()
            .HasFilter("[Status] = 'Draft'")
            .HasDatabaseName("UX_AssignmentEmails_AppraisalId_Draft");
    }
}
