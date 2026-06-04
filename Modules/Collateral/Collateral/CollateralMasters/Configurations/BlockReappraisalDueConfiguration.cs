using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class BlockReappraisalDueConfiguration : IEntityTypeConfiguration<BlockReappraisalDue>
{
    public void Configure(EntityTypeBuilder<BlockReappraisalDue> builder)
    {
        builder.ToTable("BlockReappraisalDue");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.CollateralMasterId).IsRequired();
        builder.HasIndex(r => r.CollateralMasterId)
            .IsUnique()
            .HasDatabaseName("UX_BlockReappraisalDue_CollateralMasterId");

        builder.Property(r => r.ProjectName).HasMaxLength(300);
        builder.Property(r => r.ProjectType).IsRequired().HasMaxLength(50);
        builder.Property(r => r.OldAppraisalNumber).HasMaxLength(50);
        builder.Property(r => r.ProjectSellingPrice).HasPrecision(18, 2);
        builder.Property(r => r.TotalUnits).IsRequired();
        builder.Property(r => r.RemainingUnits).IsRequired();
        builder.Property(r => r.LastAppraisedDate);
        builder.Property(r => r.DueDate).IsRequired();
        builder.Property(r => r.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("IX_BlockReappraisalDue_Status");
    }
}
