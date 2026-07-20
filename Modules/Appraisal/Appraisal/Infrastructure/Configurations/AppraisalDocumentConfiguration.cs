using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Infrastructure.Configurations;

public class AppraisalDocumentConfiguration : IEntityTypeConfiguration<AppraisalDocument>
{
    public void Configure(EntityTypeBuilder<AppraisalDocument> builder)
    {
        builder.ToTable("AppraisalDocuments");

        // Id is assigned in code via Guid.CreateVersion7() — no NEWSEQUENTIALID default.
        builder.HasKey(d => d.Id);

        builder.Property(d => d.AppraisalId).IsRequired();
        builder.Property(d => d.DocumentTypeCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.DocumentId).IsRequired();
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(255);
        builder.Property(d => d.MimeType).HasMaxLength(100);
        builder.Property(d => d.Notes).HasMaxLength(4000);
        builder.Property(d => d.SortOrder).IsRequired();
        builder.Property(d => d.UploadedByName).HasMaxLength(100);

        builder.HasIndex(d => new { d.AppraisalId, d.DocumentTypeCode });
    }
}
