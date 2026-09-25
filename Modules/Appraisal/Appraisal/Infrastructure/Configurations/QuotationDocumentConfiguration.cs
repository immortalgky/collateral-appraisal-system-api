public class QuotationDocumentConfiguration : IEntityTypeConfiguration<QuotationDocument>
{
    public void Configure(EntityTypeBuilder<QuotationDocument> builder)
    {
        builder.ToTable("QuotationDocuments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.QuotationRequestId).IsRequired();
        builder.Property(d => d.DocumentId).IsRequired();
        builder.Property(d => d.DocumentType).HasMaxLength(20).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.Source).HasMaxLength(20).IsRequired();
        // CreatedBy / CreatedAt come from Entity<> base — set by AuditableEntityInterceptor on save.
        builder.Property(d => d.CreatedBy).HasMaxLength(50);

        // NOTE: The QuotationRequest↔QuotationDocument relationship (HasMany/WithOne/FK/Cascade) is
        // declared in QuotationRequestConfiguration above. Do NOT redeclare it here — one source of truth.

        // Unique: each document can only be linked to a quotation once.
        builder.HasIndex(d => new { d.QuotationRequestId, d.DocumentId })
            .IsUnique()
            .HasDatabaseName("IX_QuotationDocuments_QuotationRequest_Document");
    }
}