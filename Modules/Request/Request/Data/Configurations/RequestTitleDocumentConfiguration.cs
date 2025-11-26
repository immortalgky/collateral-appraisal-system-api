using Microsoft.Identity.Client;

namespace Request.Data.Configurations;

public class RequestTitleDocumentConfiguration : IEntityTypeConfiguration<RequestTitleDocument>
{
    public void Configure(EntityTypeBuilder<RequestTitleDocument> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TitleId);

        builder.Property(x => x.DocumentType)
            .HasMaxLength(100);
        
        builder.Property(x => x.Filename)
            .HasMaxLength(255);
        
        builder.Property(x => x.Prefix)
            .HasMaxLength(50);

        builder.Property(x => x.Set);
        
        builder.Property(x => x.DocumentDescription)
            .HasMaxLength(500);
        
        builder.Property(x => x.FilePath)
            .HasMaxLength(255);
        
        builder.Property(x => x.CreatedWorkstation)
            .HasMaxLength(50);
        
        builder.Property(x => x.IsRequired);
        
        builder.Property(x => x.UploadedBy)
            .HasMaxLength(10);

        builder.Property(x => x.UploadedByName)
            .HasMaxLength(100);

        builder.Property(x => x.UploadedAt);
    }
}