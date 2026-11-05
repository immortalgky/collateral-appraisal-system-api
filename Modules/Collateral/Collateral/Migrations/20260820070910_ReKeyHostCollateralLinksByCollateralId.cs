using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class ReKeyHostCollateralLinksByCollateralId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.DropIndex(
                name: "IX_HostCollateralLinks_IsRedeemed",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropIndex(
                name: "UX_HostCollateralLinks_AppraisalNumber",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.AlterColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(19)",
                oldMaxLength: 19,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollateralCode",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollateralName",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocationCode",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyType",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyTypeDesc",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_AppraisalNumber",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "AppraisalNumber");

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_IsRedeemed_IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks",
                columns: new[] { "IsRedeemed", "IsMasterTitle" });

            migrationBuilder.CreateIndex(
                name: "UX_HostCollateralLinks_HostCollateralId",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "HostCollateralId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HostCollateralLinks_AppraisalNumber",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropIndex(
                name: "IX_HostCollateralLinks_IsRedeemed_IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropIndex(
                name: "UX_HostCollateralLinks_HostCollateralId",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "CollateralCode",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "CollateralName",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "IsMasterTitle",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "LocationCode",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "PropertyType",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.DropColumn(
                name: "PropertyTypeDesc",
                schema: "collateral",
                table: "HostCollateralLinks");

            migrationBuilder.AlterColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "HostCollateralLinks",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(19)",
                oldMaxLength: 19);

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_IsRedeemed",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "IsRedeemed");

            migrationBuilder.CreateIndex(
                name: "UX_HostCollateralLinks_AppraisalNumber",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "AppraisalNumber",
                unique: true);
        }
    }
}
