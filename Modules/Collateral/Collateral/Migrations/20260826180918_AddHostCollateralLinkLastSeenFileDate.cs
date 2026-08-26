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

            // Existing rows came from the most recent COLLATLINK file that was ingested, so they are
            // all part of the current active set. Left at the 0001-01-01 default every one of them
            // would read as "no longer reported by AS400" the moment a reader applied the active
            // filter — the regulatory export would empty out and the outbound result would find no
            // collateral id for anything, until the next monthly file happened to arrive.
            //
            // MAX over the whole table rather than each row's own UpdatedAt: a row the feed keeps
            // restating unchanged is never written, so its UpdatedAt can be months behind while the
            // row is perfectly current.
            migrationBuilder.Sql(@"
                UPDATE collateral.HostCollateralLinks
                SET LastSeenFileDate = CAST((SELECT MAX(UpdatedAt) FROM collateral.HostCollateralLinks) AS date)
                WHERE EXISTS (SELECT 1 FROM collateral.HostCollateralLinks);");

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
