using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMasterAndParentMasterId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMaster",
                schema: "collateral",
                table: "CollateralMasters",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentMasterId",
                schema: "collateral",
                table: "CollateralMasters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_IsMaster",
                schema: "collateral",
                table: "CollateralMasters",
                column: "IsMaster");

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_ParentMasterId",
                schema: "collateral",
                table: "CollateralMasters",
                column: "ParentMasterId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollateralMasters_ParentMaster",
                schema: "collateral",
                table: "CollateralMasters",
                column: "ParentMasterId",
                principalSchema: "collateral",
                principalTable: "CollateralMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollateralMasters_ParentMaster",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropIndex(
                name: "IX_CollateralMasters_IsMaster",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropIndex(
                name: "IX_CollateralMasters_ParentMasterId",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "IsMaster",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "ParentMasterId",
                schema: "collateral",
                table: "CollateralMasters");
        }
    }
}
