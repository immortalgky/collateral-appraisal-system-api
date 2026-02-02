using Request.Domain.RequestTitles;
using Shared.Data.Extensions;

namespace Request.Infrastructure.Configurations;

public class TitleDocumentConfiguration
    : IOwnedEntityConfiguration<RequestTitle, TitleDocument>
{
    public void Configure(OwnedNavigationBuilder<RequestTitle, TitleDocument> builder)
    {
        builder.ToTable("RequestTitleDocuments");
        builder.WithOwner().HasForeignKey(d => d.TitleId);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.DocumentType).HasMaxLength(100);
        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.Property(d => d.Prefix).HasMaxLength(50);
        builder.Property(d => d.Notes).HasMaxLength(500);
        builder.Property(d => d.FilePath).HasMaxLength(255);
        builder.Property(d => d.UploadedBy).HasMaxLength(10);
        builder.Property(d => d.UploadedByName).HasMaxLength(100);

        builder.HasIndex(d => new { d.TitleId, d.DocumentId })
            .IsUnique()
            .HasFilter("[DocumentId] IS NOT NULL")
            .HasDatabaseName("IX_TitleDocument_Title_Document");
    }
}