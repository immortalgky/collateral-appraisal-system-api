namespace Document.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Documents.Models.Document>
{
    public void Configure(EntityTypeBuilder<Documents.Models.Document> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("DocumentId").UseIdentityColumn();

        builder.ToTable("Documents");
        builder.Property(p => p.DocType).HasMaxLength(10);
        builder.Property(p => p.Filename).HasMaxLength(255);
        builder.Property(p => p.Prefix).HasMaxLength(50);
        builder.Property(p => p.Comment).HasMaxLength(3000);
        builder.Property(p => p.FilePath).HasMaxLength(255);
    }
}