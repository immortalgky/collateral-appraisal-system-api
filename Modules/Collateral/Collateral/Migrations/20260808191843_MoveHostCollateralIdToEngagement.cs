using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class MoveHostCollateralIdToEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecordDate",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordIndicator",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagements_AppraisalNumber",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "AppraisalNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagements_HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");

            // ── Move the existing data before dropping the master column ──────────────────
            //
            // Why this SQL lives in a migration when data normally belongs in
            // Database/Migration/Scripts:
            //
            //   1. Seed / reference data → belongs in a DbUp script only. It is environment-
            //      dependent, awkward to re-run, and a failure blocks provisioning an entire
            //      database. (Real example: 20260317002400_SeedData_MarketComparable.sql prevented
            //      Tests/Integration from building a fresh DB at all.)
            //   2. Data movement that is part of a schema change (copy column A→B before dropping A)
            //      → cannot be separated, because DbUp always runs AFTER EF migrations. Moving this
            //      to DbUp would mean the source column is already gone and the data lost for good.
            //
            // This block is case 2, and it is structurally safe on a fresh database: the
            // `m.HostCollateralId IS NOT NULL` predicate matches zero rows there, so it is a no-op
            // and cannot block provisioning the way a seed script can.
            //
            // A more conservative alternative is a two-release expand → migrate → contract: add the
            // new columns and copy via DbUp now, drop the old column next release. The cost is one
            // release with an unused column left in place.
            //
            // The copy is required, or both outbound files go empty immediately after deploy:
            //   COLLATERAL_RESULT filters on e.HostCollateralId != null
            //   vw_RegulatoryExport filters on the engagement-level host id
            // and the only other populator (HostCollateralIdBackfillJob) is deleted in the same
            // change, so nothing could recover the data after DropColumn.
            //
            // The master column meant "this collateral's current id", and the previous export
            // emitted every engagement of a master that had one — so the value is copied to every
            // engagement of that master to preserve the previous eligibility set. RecordIndicator is
            // set to 'D' because AS400 only mints an id at drawdown, so having one means it was
            // pledged. RecordDate is left NULL because it cannot be recovered.
            //
            // The nightly COLLATLINK feed overwrites these values with real data on its next run.
            migrationBuilder.Sql("""
                UPDATE e
                SET    e.HostCollateralId = m.HostCollateralId,
                       e.RecordIndicator  = 'D'
                FROM   collateral.CollateralEngagements e
                JOIN   collateral.CollateralMasters m ON m.Id = e.CollateralMasterId
                WHERE  m.HostCollateralId IS NOT NULL
                  AND  e.HostCollateralId IS NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CollateralMasters_HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollateralEngagements_AppraisalNumber",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropIndex(
                name: "IX_CollateralEngagements_HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "RecordDate",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "RecordIndicator",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.AddColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");
        }
    }
}
