using System;

namespace Request.Data.Configurations;

public class RequestDocumentConfiguration : IEntityTypeConfiguration<RequestDocuments.Models.RequestDocument>
{
    public void Configure(EntityTypeBuilder<RequestDocuments.Models.RequestDocument> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.RequestId);
        builder.Property(p => p.DocumentId);

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

