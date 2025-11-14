namespace Request.Data.Configurations;

public class RequestTitleDocumentConfiguration : IEntityTypeConfiguration<RequestTitleDocument>
{
    public void Configure(EntityTypeBuilder<RequestTitleDocument> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TitleId);

        // builder.Property(x => x.DocumentId);
        // builder.HasOne(x => x.Document)
        //     .WithMany(x => x.RequestTitleDocuments)
        //     .HasForeignKey(x => x.DocumentId)
        //     .OnDelete(DeleteBehavior.Cascade)
        //     .HasConstraintName("FK_RequestTitleDocument_Document");

        builder.Property(x => x.DocumentType)
            .HasMaxLength(100);
        
        builder.Property(x => x.IsRequired);
        
        builder.Property(x => x.DocumentDescription)
            .HasMaxLength(500);
        
        builder.Property(x => x.UploadedBy)
            .HasMaxLength(10);

        builder.Property(x => x.UploadedByName)
            .HasMaxLength(100);

        builder.Property(x => x.UploadedAt);
    }
}