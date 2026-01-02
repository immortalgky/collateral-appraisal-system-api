namespace Request.Infrastructure.Configurations;

public class RequestDocumentConfiguration : IEntityTypeConfiguration<RequestDocument>
{
    public void Configure(EntityTypeBuilder<RequestDocument> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DocumentType).HasMaxLength(10);
        builder.Property(p => p.FileName).HasMaxLength(255);
        builder.Property(p => p.Prefix).HasMaxLength(50);
        builder.Property(p => p.FilePath).HasMaxLength(500);
        builder.Property(p => p.Source).HasMaxLength(10);
        builder.Property(p => p.Notes).HasMaxLength(4000);
        builder.Property(p => p.UploadedBy).HasMaxLength(10);
        builder.Property(p => p.UploadedByName).HasMaxLength(100);

        // Unique indexes for duplicate prevention
        builder.HasIndex(d => new { d.RequestId, d.DocumentId })
            .IsUnique()
            .HasFilter("[DocumentId] IS NOT NULL")
            .HasDatabaseName("IX_RequestDocument_Request_Document");
    }
}