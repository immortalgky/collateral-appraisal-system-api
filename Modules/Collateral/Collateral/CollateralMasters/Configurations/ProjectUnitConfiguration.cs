using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class ProjectUnitConfiguration : IEntityTypeConfiguration<ProjectUnit>
{
    public void Configure(EntityTypeBuilder<ProjectUnit> builder)
    {
        builder.ToTable("ProjectUnits");

        // Primary Key — NEWSEQUENTIALID() server fallback; application always sets via Guid.CreateVersion7()
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // CollateralMasterId FK — relationship and cascade are configured via ProjectDetailConfiguration
        // (ProjectDetail.Units → ProjectUnit) to avoid duplicate FK constraint names.
        builder.Property(e => e.CollateralMasterId).IsRequired();
        builder.HasIndex(e => e.CollateralMasterId);

        // Sequence
        builder.Property(e => e.SequenceNumber).IsRequired();
        builder.HasIndex(e => new { e.CollateralMasterId, e.SequenceNumber })
            .HasDatabaseName("IX_ProjectUnits_Master_Sequence");

        // Common fields
        builder.Property(e => e.ModelType).HasMaxLength(200);
        builder.Property(e => e.UsableArea).HasPrecision(10, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);

        // Condo-side fields
        builder.Property(e => e.TowerName).HasMaxLength(200);
        builder.Property(e => e.CondoRegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.RoomNumber).HasMaxLength(50);

        // LandAndBuilding-side fields
        builder.Property(e => e.PlotNumber).HasMaxLength(100);
        builder.Property(e => e.HouseNumber).HasMaxLength(100);
        builder.Property(e => e.LandArea).HasPrecision(10, 2);

        // Sale tracking
        builder.Property(e => e.IsSold)
            .IsRequired()
            .HasDefaultValue(false);

        // Stored as enum NAME string ("Cash" / "Loan") — consistent with Appraisal module convention.
        // This allows SQL reads and JSON wire formats to use human-readable values without a CASE expression.
        builder.Property(e => e.PurchaseBy)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired(false);

        builder.Property(e => e.LoanBankName)
            .HasMaxLength(200)
            .IsRequired(false);

        // Reference value
        builder.Property(e => e.LastAppraisedValue)
            .HasPrecision(18, 2)
            .IsRequired(false);
    }
}
