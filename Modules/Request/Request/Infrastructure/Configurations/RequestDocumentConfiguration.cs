using Shared.Data.Extensions;

namespace Request.Infrastructure.Configurations;

public class RequestDocumentConfiguration
    : IOwnedEntityConfiguration<Domain.Requests.Request, RequestDocument>
{
    public void Configure(OwnedNavigationBuilder<Domain.Requests.Request, RequestDocument> builder)
    {
        builder.ToTable("RequestDocuments");
        builder.WithOwner().HasForeignKey(d => d.RequestId);
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType).HasMaxLength(10);
        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.Property(d => d.Prefix).HasMaxLength(50);
        builder.Property(d => d.Notes).HasMaxLength(4000);
        builder.Property(d => d.FilePath).HasMaxLength(500);
        builder.Property(d => d.Source).HasMaxLength(10);
        builder.Property(d => d.UploadedBy).HasMaxLength(10);
        builder.Property(d => d.UploadedByName).HasMaxLength(100);

        builder.HasIndex(d => new { d.RequestId, d.DocumentId })
            .IsUnique()
            .HasFilter("[DocumentId] IS NOT NULL")
            .HasDatabaseName("IX_RequestDocument_Request_Document");
    }
}