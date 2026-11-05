using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class MasterTitleFlagRaw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.DropIndex(
                name: "IX_HostCollateralLinks_IsRedeemed_IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.AddColumn<string>(
                name: "MasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_IsRedeemed_MasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks",
                columns: new[] { "IsRedeemed", "MasterTitle" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HostCollateralLinks_IsRedeemed_MasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "MasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.AddColumn<bool>(
                name: "IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_IsRedeemed_IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks",
                columns: new[] { "IsRedeemed", "IsMasterTitle" });
        }
    }
}
