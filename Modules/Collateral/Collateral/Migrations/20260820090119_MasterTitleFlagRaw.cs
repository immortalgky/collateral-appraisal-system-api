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
            // The bool this replaces could not tell an explicit 'N' from a row that never stated the
            // flag — both landed as false — so the existing rows cannot be converted, only re-read.
            // The distinction is the point of the change: the regulatory export reports 'N' and drops
            // the unstated. Emptying the table is the only honest starting point; the nightly
            // COLLATLINK ingest refills it completely on its next run.
            migrationBuilder.Sql("DELETE FROM collateral.HostCollateralLinks;");

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
