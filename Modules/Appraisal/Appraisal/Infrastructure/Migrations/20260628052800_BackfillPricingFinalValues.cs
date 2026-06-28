using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPricingFinalValues : Migration
    {
        // Backfill: historical appraisals stored each method's final value only in the per-method
        // table (Income/Leasehold/ProfitRent/Hypothesis) or as MethodValue (Machinery/BuildingCost),
        // with NO PricingFinalValues row. This populates that shared row so PricingFinalValues becomes
        // the single source of truth before Phase C drops the per-method final columns.
        //
        // Idempotent: every INSERT is guarded by NOT EXISTS on PricingMethodId, so re-running (SIT/
        // UAT/Prod, or after a partial failure) inserts nothing the second time. Comparative methods
        // (WQS/SaleGrid/DirectComparison) already have a row and are skipped. Id is omitted so the
        // column default NEWSEQUENTIALID() applies. Runs after the BuildingValue rename, so the
        // HasBuildingValue column exists.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Income — preserve adjust + appraisal price for a lossless Phase C drop.
            migrationBuilder.Sql("""
                INSERT INTO appraisal.PricingFinalValues
                    (PricingMethodId, FinalValue, FinalValueRounded, FinalValueAdjusted, AppraisalPrice,
                     IncludeLandArea, HasBuildingValue, CreatedAt, CreatedBy)
                SELECT m.Id,
                       COALESCE(i.FinalValue, 0), COALESCE(i.FinalValueRounded, 0),
                       i.FinalValueAdjust, i.AppraisalPriceRounded,
                       1, 0, GETDATE(), 'BACKFILL'
                FROM   appraisal.PricingAnalysisMethods m
                JOIN   appraisal.IncomeAnalyses i ON i.PricingAnalysisMethodId = m.Id
                WHERE  NOT EXISTS (SELECT 1 FROM appraisal.PricingFinalValues f WHERE f.PricingMethodId = m.Id);
                """);

            // 2. Leasehold
            migrationBuilder.Sql("""
                INSERT INTO appraisal.PricingFinalValues
                    (PricingMethodId, FinalValue, FinalValueRounded, IncludeLandArea, HasBuildingValue, CreatedAt, CreatedBy)
                SELECT m.Id, l.FinalValue, l.FinalValueRounded, 1, 0, GETDATE(), 'BACKFILL'
                FROM   appraisal.PricingAnalysisMethods m
                JOIN   appraisal.LeaseholdAnalyses l ON l.PricingMethodId = m.Id
                WHERE  NOT EXISTS (SELECT 1 FROM appraisal.PricingFinalValues f WHERE f.PricingMethodId = m.Id);
                """);

            // 3. ProfitRent (only FinalValueRounded exists)
            migrationBuilder.Sql("""
                INSERT INTO appraisal.PricingFinalValues
                    (PricingMethodId, FinalValue, FinalValueRounded, IncludeLandArea, HasBuildingValue, CreatedAt, CreatedBy)
                SELECT m.Id, p.FinalValueRounded, p.FinalValueRounded, 1, 0, GETDATE(), 'BACKFILL'
                FROM   appraisal.PricingAnalysisMethods m
                JOIN   appraisal.ProfitRentAnalyses p ON p.PricingMethodId = m.Id
                WHERE  NOT EXISTS (SELECT 1 FROM appraisal.PricingFinalValues f WHERE f.PricingMethodId = m.Id);
                """);

            // 4. Hypothesis — final lives in the owned summary (LB FSD C81 / Condo FSD E58); one is populated per variant.
            migrationBuilder.Sql("""
                INSERT INTO appraisal.PricingFinalValues
                    (PricingMethodId, FinalValue, FinalValueRounded, IncludeLandArea, HasBuildingValue, CreatedAt, CreatedBy)
                SELECT m.Id,
                       COALESCE(h.LandBuildingSummary_TotalAssetValueRounded, h.CondominiumSummary_TotalAssetValueRounded, 0),
                       COALESCE(h.LandBuildingSummary_TotalAssetValueRounded, h.CondominiumSummary_TotalAssetValueRounded, 0),
                       1, 0, GETDATE(), 'BACKFILL'
                FROM   appraisal.PricingAnalysisMethods m
                JOIN   appraisal.HypothesisAnalyses h ON h.PricingMethodId = m.Id
                WHERE  NOT EXISTS (SELECT 1 FROM appraisal.PricingFinalValues f WHERE f.PricingMethodId = m.Id);
                """);

            // 5. Generic catch-all — Machinery, BuildingCost, and any other method that has a committed
            //    MethodValue but still no row (its final equals MethodValue). Runs last; the NOT EXISTS
            //    guard means methods filled by 1–4 above (and pre-existing comparative rows) are skipped.
            migrationBuilder.Sql("""
                INSERT INTO appraisal.PricingFinalValues
                    (PricingMethodId, FinalValue, FinalValueRounded, IncludeLandArea, HasBuildingValue, CreatedAt, CreatedBy)
                SELECT m.Id, m.MethodValue, m.MethodValue, 1, 0, GETDATE(), 'BACKFILL'
                FROM   appraisal.PricingAnalysisMethods m
                WHERE  m.MethodValue IS NOT NULL
                  AND  NOT EXISTS (SELECT 1 FROM appraisal.PricingFinalValues f WHERE f.PricingMethodId = m.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse only the rows this backfill created.
            migrationBuilder.Sql(
                "DELETE FROM appraisal.PricingFinalValues WHERE CreatedBy = 'BACKFILL';");
        }
    }
}
