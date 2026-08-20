using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralEngagementConfiguration : IEntityTypeConfiguration<CollateralEngagement>
{
    public void Configure(EntityTypeBuilder<CollateralEngagement> builder)
    {
        builder.ToTable("CollateralEngagements");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CollateralMasterId).IsRequired();
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.Property(e => e.AppraisalNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.RequestId).IsRequired();
        // ← request.Requests.RequestNumber nvarchar(255)
        builder.Property(e => e.RequestNumber).IsRequired().HasMaxLength(255);
        // PropertyId column dropped in PR-4 (engagement is now per-appraisal, not per-property).
        // ← appraisal.Appraisals.AppraisalType nvarchar(50)
        builder.Property(e => e.AppraisalType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.AppraisalDate).IsRequired();
        // AppraisedValue column dropped in PR-4 (values live on master detail rows + snapshot).
        builder.Property(e => e.AppraiserUserId).HasMaxLength(100);
        builder.Property(e => e.AppraisalCompanyName).HasMaxLength(200);
        builder.Property(e => e.ConstructionInspectionFeeAmount).HasPrecision(18, 2);
        builder.Property(e => e.Snapshot).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedAt).IsRequired().HasColumnName("CreatedAt");

        // Engagement-time history fields (nullable — pre-existing engagements won't have values).
        builder.Property(e => e.AppraisedCollateralType).HasMaxLength(30);
        builder.Property(e => e.LandAreaInSqWa).HasPrecision(18, 4);

        // Outbound Collateral Result interface enrichment fields.
        builder.Property(e => e.ForcedSaleValue).HasPrecision(18, 2);
        // No single source column: GetAppraisalForCollateralQueryHandler builds this as
        // CONCAT(FirstName, ' ', LastName) over auth.AspNetUsers, so it can reach 100+1+100 = 201.
        builder.Property(e => e.InternalAppraiserName).HasMaxLength(255);
        builder.Property(e => e.AppraisalValue).HasPrecision(18, 2);
        builder.Property(e => e.LandValue).HasPrecision(18, 2);
        builder.Property(e => e.BuildingValue).HasPrecision(18, 2);
        // Part-built value frozen at engagement time — see CollateralEngagement.CurrentValue.
        builder.Property(e => e.CurrentValue).HasPrecision(18, 2);
        builder.Property(e => e.ConstructionProgressPercent).HasPrecision(7, 4);

        // Mirrors the filtered IX_LandDetails_UnderConstruction it replaces: the collateral catalog
        // filters on this flag (GetCollateralCatalogQueryHandler) and only the =1 side is selective.
        builder.HasIndex(e => e.IsUnderConstruction)
            .HasFilter("[IsUnderConstruction] = 1")
            .HasDatabaseName("IX_CollateralEngagements_UnderConstruction");
        // HostCompanyCode from auth.Companies — max length matches CompanyConfiguration.HostCompanyCode (10).
        builder.Property(e => e.AppraisalCompanyCode).HasMaxLength(10);

        // AS400 host state moved to CollateralMasters — see the note in CollateralEngagement.cs.
        // This index stays: the feed still resolves an incoming row to its master by AppraisalNumber.
        builder.HasIndex(e => e.AppraisalNumber)
            .HasDatabaseName("IX_CollateralEngagements_AppraisalNumber");

        // PR-4: Idempotency — one engagement per appraisal (unique by AppraisalId).
        // Replaces the old (AppraisalId, PropertyId) composite unique index.
        builder.HasIndex(e => e.AppraisalId)
            .IsUnique()
            .HasDatabaseName("UX_CollateralEngagements_Appraisal");

        // History pagination: newest first per master
        builder.HasIndex(e => new { e.CollateralMasterId, e.AppraisalDate })
            .HasDatabaseName("IX_CollateralEngagements_Master_Date");

        // Appeal company exclusion lookup
        builder.HasIndex(e => e.AppraisalCompanyId)
            .HasDatabaseName("IX_CollateralEngagements_AppraisalCompanyId");

        // Buildings child collection navigation
        builder.HasMany(e => e.Buildings)
            .WithOne()
            .HasForeignKey(b => b.EngagementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CollateralEngagementBuildings_Engagement");
        builder.Navigation(e => e.Buildings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
