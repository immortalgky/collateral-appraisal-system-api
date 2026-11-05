using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddHostCollateralLinkLastSeenFileDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastSeenFileDate",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_LastSeenFileDate",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "LastSeenFileDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HostCollateralLinks_LastSeenFileDate",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "LastSeenFileDate",
                schema: "collateral",
                table: "HostCollateralLinks");
        }
    }
}
