using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPricingFinalValues : Migration
    {
        // Historical backfill; the SQL was removed and Up() is now a no-op. It had inserted a
        // PricingFinalValues row for appraisals that stored a method's final value only in the
        // per-method table (Income/Leasehold/ProfitRent/Hypothesis) or as MethodValue
        // (Machinery/BuildingCost). Down() still deletes the rows created with CreatedBy = 'BACKFILL'.
        //
        // The removed INSERTs were guarded by NOT EXISTS on PricingMethodId and skipped comparative
        // methods (WQS/SaleGrid/DirectComparison), which already had a row.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
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
