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

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

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
