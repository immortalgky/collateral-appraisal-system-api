namespace Request.Data.Configurations;

public class RequestDocumentConfiguration : IEntityTypeConfiguration<RequestDocument>
{
    public void Configure(EntityTypeBuilder<RequestDocument> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("DocumentId").UseIdentityColumn();

        builder.ToTable("RequestDocuments");
        builder.Property(p => p.DocType).HasMaxLength(10);
        builder.Property(p => p.FileName).HasMaxLength(255);
        builder.Property(p => p.Prefix).HasMaxLength(50);
        builder.Property(p => p.Comment).HasMaxLength(3000);
        builder.Property(p => p.FilePath).HasMaxLength(255);
    }
}