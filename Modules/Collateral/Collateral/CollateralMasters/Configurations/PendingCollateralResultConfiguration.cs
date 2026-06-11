using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class PendingCollateralResultConfiguration : IEntityTypeConfiguration<PendingCollateralResult>
{
    public void Configure(EntityTypeBuilder<PendingCollateralResult> builder)
    {
        builder.ToTable("PendingCollateralResults");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AppraisalId).IsRequired();
        builder.Property(r => r.AppraisalNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.HostCollateralId).HasMaxLength(19);
        builder.Property(r => r.RejectedAt).IsRequired();
        builder.Property(r => r.SentAt);
        builder.Property(r => r.SentFileName).HasMaxLength(100);

        // Idempotency guard: one spool row per rejected appraisal.
        builder.HasIndex(r => r.AppraisalId)
            .IsUnique()
            .HasDatabaseName("UX_PendingCollateralResults_Appraisal");

        // Export query filters on SentAt IS NULL; partial index keeps it narrow.
        builder.HasIndex(r => r.SentAt)
            .HasDatabaseName("IX_PendingCollateralResults_SentAt")
            .HasFilter("[SentAt] IS NULL");
    }
}
