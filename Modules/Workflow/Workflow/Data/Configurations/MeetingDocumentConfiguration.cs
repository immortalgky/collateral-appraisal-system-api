using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Meetings.Domain;

namespace Workflow.Data.Configurations;

public class MeetingDocumentConfiguration : IEntityTypeConfiguration<MeetingDocument>
{
    public void Configure(EntityTypeBuilder<MeetingDocument> builder)
    {
        builder.ToTable("MeetingDocuments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.MeetingId).IsRequired();
        builder.Property(d => d.DocumentId).IsRequired();
        builder.Property(d => d.DocumentType).HasMaxLength(20).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.Source).HasMaxLength(20).IsRequired();
        // CreatedBy / CreatedAt come from Entity<> base — set by AuditableEntityInterceptor on save.
        builder.Property(d => d.CreatedBy).HasMaxLength(50);

        // NOTE: The Meeting↔MeetingDocument relationship (HasMany/WithOne/FK/Cascade) is declared
        // in MeetingConfiguration.cs. Do NOT redeclare it here — one source of truth.

        // Unique: each document can only be linked to a meeting once.
        builder.HasIndex(d => new { d.MeetingId, d.DocumentId })
            .IsUnique()
            .HasDatabaseName("IX_MeetingDocuments_Meeting_Document");
    }
}
