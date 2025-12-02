using System;

namespace Request.Data.Configurations;

public class RequestDocumentConfiguration : IEntityTypeConfiguration<RequestDocuments.Models.RequestDocument>
{
    public void Configure(EntityTypeBuilder<RequestDocuments.Models.RequestDocument> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.RequestId);
        builder.Property(p => p.DocumentId);
        builder.Property(p => p.FileName).HasMaxLength(255).HasColumnName("FileName");
        builder.Property(p => p.Prefix).HasMaxLength(50).HasColumnName("Prefix");
        builder.Property(p => p.Set).HasColumnName("Set");
        builder.Property(p => p.FilePath).HasMaxLength(500).HasColumnName("FilePath");
        builder.Property(p => p.DocumentFollowUp).HasColumnName("DocumentFollowUp");

        builder.OwnsOne(p => p.DocumentClassification, documentClassification =>
        {
            documentClassification.Property(p => p.DocumentType).HasMaxLength(100).HasColumnName("DocumentType");
            documentClassification.Property(p => p.IsRequired).HasColumnName("IsRequire");
        });
        builder.Property(p => p.DocumentDescription).HasMaxLength(500).HasColumnName("DocumentDescription");

        builder.OwnsOne(p => p.UploadInfo, uploadInfo =>
        {
            uploadInfo.Property(p => p.UploadedBy).HasColumnName("UploadedBy");
            uploadInfo.Property(p => p.UploadedByName).HasColumnName("UploadedByName");
            uploadInfo.Property(p => p.UploadedAt).HasColumnName("UploadedAt");
        });
    }
}

