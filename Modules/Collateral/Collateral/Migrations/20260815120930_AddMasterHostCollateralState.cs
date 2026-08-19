using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterHostCollateralState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRedeemed",
                schema: "collateral",
                table: "CollateralMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RedeemedDate",
                schema: "collateral",
                table: "CollateralMasters",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollateralMasters_HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "IsRedeemed",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "RedeemedDate",
                schema: "collateral",
                table: "CollateralMasters");
        }
    }
}
