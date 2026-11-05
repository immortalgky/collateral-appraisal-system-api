using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class DropEngagementHostCollateralState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "IX_CollateralEngagements_HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");

            // The columns come back empty. Down() restores the SHAPE, not the data: the master holds
            // one current state per collateral and cannot say which appraisal each value belonged
            // to, so spreading it back across engagements would invent history. Re-run
            // HostCollateralIdBackfillJob or wait for the next HOST_COLLATERAL_LINK file instead.
        }
    }
}
