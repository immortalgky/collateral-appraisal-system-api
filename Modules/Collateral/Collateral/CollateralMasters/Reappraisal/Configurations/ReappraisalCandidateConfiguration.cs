using Collateral.CollateralMasters.Reappraisal;

namespace Collateral.CollateralMasters.Reappraisal.Configurations;

public class ReappraisalCandidateConfiguration : IEntityTypeConfiguration<ReappraisalCandidate>
{
    public void Configure(EntityTypeBuilder<ReappraisalCandidate> builder)
    {
        builder.ToTable("ReappraisalCandidates");
        builder.HasKey(p => p.Id);

        // Ingestion metadata
        builder.Property(p => p.SourceFileName).IsRequired().HasMaxLength(260);
        builder.Property(p => p.SourceFileDate).IsRequired();
        builder.Property(p => p.EffectiveDate).IsRequired();
        builder.Property(p => p.IngestedAt).IsRequired();
        builder.Property(p => p.RowHash).IsRequired().HasMaxLength(64); // SHA-256 hex = 64 chars

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Fixed-width detail fields
        builder.Property(p => p.ReviewType).IsRequired().HasMaxLength(1);
        builder.Property(p => p.ReviewDate).IsRequired();
        builder.Property(p => p.CollateralId).IsRequired().HasMaxLength(19);
        builder.Property(p => p.SurveyNumber).IsRequired().HasMaxLength(10);
        builder.Property(p => p.CollateralCode).IsRequired().HasMaxLength(3);
        builder.Property(p => p.CollateralCategory).IsRequired().HasMaxLength(5);

        builder.Property(p => p.CollateralName).HasMaxLength(40);
        builder.Property(p => p.CollateralAddress).HasMaxLength(120);
        builder.Property(p => p.CifNumber).IsRequired().HasMaxLength(19);
        builder.Property(p => p.CifName).HasMaxLength(20);
        builder.Property(p => p.AoCode).HasMaxLength(10);
        builder.Property(p => p.AoName).HasMaxLength(40);
        builder.Property(p => p.TitleNumber).HasMaxLength(20);

        builder.Property(p => p.CurrentValue).HasPrecision(15, 2);
        builder.Property(p => p.ValuationDate);

        builder.Property(p => p.InternalExternal).HasMaxLength(1);
        builder.Property(p => p.BusinessSize).HasMaxLength(1);
        builder.Property(p => p.BusinessSizeDesc).HasMaxLength(40);

        builder.Property(p => p.MortgageAmount).HasPrecision(15, 2);
        builder.Property(p => p.PastDueDay);
        builder.Property(p => p.ApplicationNumber).HasMaxLength(19);
        builder.Property(p => p.FacilityCode).HasMaxLength(3);
        builder.Property(p => p.FacilitySequence).HasMaxLength(19);
        builder.Property(p => p.CpNumber).HasMaxLength(16);
        builder.Property(p => p.CarCode).HasMaxLength(3);
        builder.Property(p => p.FacilityLimit).HasPrecision(15, 2);

        builder.Property(p => p.FlagLessAge4Y).HasMaxLength(1);
        builder.Property(p => p.FlagGreaterAge4Y).HasMaxLength(1);
        builder.Property(p => p.CountAgeingDate).HasMaxLength(10);

        builder.Property(p => p.CollateralDescription).HasMaxLength(50);
        builder.Property(p => p.ExternalValuerName).HasMaxLength(40);
        builder.Property(p => p.InternalValuerName).HasMaxLength(40);
        builder.Property(p => p.SllOver100M).HasMaxLength(1);
        builder.Property(p => p.SllDescription).HasMaxLength(50);

        // Trailing extension fields (pos 630–649).
        builder.Property(p => p.Stage).HasMaxLength(1);
        builder.Property(p => p.IBGRetail).HasMaxLength(10);
        builder.Property(p => p.Group).HasMaxLength(1);
        builder.Property(p => p.EffectiveDateAppraisal);

        // Geo coordinates (enriched post-ingest; NULL when no SurveyNumber match)
        builder.Property(p => p.Latitude).HasPrecision(10, 7);
        builder.Property(p => p.Longitude).HasPrecision(10, 7);
        // GeoPoint is a persisted computed column (geography::Point) added via raw-SQL migration;
        // it is NOT mapped in EF to avoid NetTopologySuite dependency.

        // Unique index: natural deduplication key for upsert / re-ingest idempotency.
        builder.HasIndex(p => new { p.SourceFileDate, p.CollateralId, p.SurveyNumber })
            .IsUnique()
            .HasDatabaseName("IX_ReappraisalCandidate_FileDate_CollateralId_SurveyNumber");

        // Supporting indexes for the list query filters.
        builder.HasIndex(p => p.Status)
            .HasFilter("[Status] = 'Pending'")
            .HasDatabaseName("IX_ReappraisalCandidate_Status_Pending");

        builder.HasIndex(p => p.ReviewDate)
            .HasDatabaseName("IX_ReappraisalCandidate_ReviewDate");
    }
}
