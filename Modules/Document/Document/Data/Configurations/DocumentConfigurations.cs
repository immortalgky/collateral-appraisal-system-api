namespace Document.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Documents.Models.Document>
{
    public void Configure(EntityTypeBuilder<Documents.Models.Document> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DocumentType).HasMaxLength(10);
        builder.Property(p => p.DocumentCategory).HasMaxLength(10);
        builder.Property(p => p.FileName).HasMaxLength(255);
        builder.Property(p => p.FileExtension).HasMaxLength(10);
        builder.Property(p => p.MimeType).HasMaxLength(100);
        builder.Property(p => p.StoragePath).HasMaxLength(500);
        builder.Property(p => p.StorageUrl).HasMaxLength(500);
        builder.Property(p => p.UploadedBy).HasMaxLength(10);
        builder.Property(p => p.UploadedByName).HasMaxLength(200);
        builder.Property(p => p.OrphanedReason).HasMaxLength(200);
        builder.Property(p => p.AccessLevel).HasMaxLength(50);
        builder.Property(p => p.ArchivedBy).HasMaxLength(10);
        builder.Property(p => p.ArchivedByName).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Checksum).HasMaxLength(100);
        builder.Property(p => p.ChecksumAlgorithm).HasMaxLength(20);
        builder.Property(p => p.DeletedBy).HasMaxLength(10);

        builder.HasIndex(p => p.DocumentType).HasFilter("IsDeleted = 0");
        builder.HasIndex(p => p.DocumentCategory).HasFilter("IsDeleted = 0");
        builder.HasIndex(p => p.Checksum).HasFilter("IsDeleted = 0");
    }
}