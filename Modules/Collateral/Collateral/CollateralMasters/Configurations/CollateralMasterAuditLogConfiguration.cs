using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralMasterAuditLogConfiguration : IEntityTypeConfiguration<CollateralMasterAuditLog>
{
    public void Configure(EntityTypeBuilder<CollateralMasterAuditLog> builder)
    {
        builder.ToTable("CollateralMasterAuditLogs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.CollateralMasterId).IsRequired();
        builder.Property(a => a.Action).IsRequired().HasMaxLength(50);
        builder.Property(a => a.ChangedFields).HasColumnType("nvarchar(max)");
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ChangedBy).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ChangedAt).IsRequired();

        builder.HasIndex(a => new { a.CollateralMasterId, a.ChangedAt })
            .HasDatabaseName("IX_CollateralMasterAuditLogs_Master_ChangedAt");
    }
}
