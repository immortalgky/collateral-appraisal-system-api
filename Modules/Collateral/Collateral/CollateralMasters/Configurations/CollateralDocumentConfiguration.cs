using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralDocumentConfiguration : IEntityTypeConfiguration<CollateralDocument>
{
    public void Configure(EntityTypeBuilder<CollateralDocument> builder)
    {
        builder.ToTable("CollateralDocuments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.CollateralMasterId).IsRequired();
        builder.Property(d => d.DocumentType).IsRequired().HasMaxLength(50);
        builder.Property(d => d.DocumentId).IsRequired();
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(260);
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);

        // Audit fields — values stamped by AuditableEntityInterceptor.
        // Max-length overrides match CollateralMasterConfiguration (100) — wider than
        // the global 10-char convention because user IDs in this system can be longer.
        builder.Property(d => d.CreatedAt).HasColumnName("CreatedAt");
        builder.Property(d => d.UpdatedAt).HasColumnName("UpdatedAt");
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        // Workstation fields are captured by IEntity but not used in this context.
        builder.Property(d => d.CreatedWorkstation).HasMaxLength(100).IsRequired(false);
        builder.Property(d => d.UpdatedWorkstation).HasMaxLength(100).IsRequired(false);

        // Filtered indexes — active documents only (mirrors the DDL spec).
        builder.HasIndex(d => d.CollateralMasterId)
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_CollateralDocuments_CollateralMasterId");

        builder.HasIndex(d => new { d.CollateralMasterId, d.DocumentType })
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_CollateralDocuments_DocumentType");
    }
}
