using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddCollateralResultExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LifeYear",
                schema: "collateral",
                table: "MachineDetails",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingValue",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForcedSaleValue",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalAppraiserName",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandValue",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollateralResultLogs",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CollateralId = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralResultLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMasters_HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CollateralResultLogs_Appraisal",
                schema: "collateral",
                table: "CollateralResultLogs",
                column: "AppraisalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollateralResultLogs",
                schema: "collateral");

            migrationBuilder.DropIndex(
                name: "IX_CollateralMasters_HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "LifeYear",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "BuildingValue",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "ForcedSaleValue",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "InternalAppraiserName",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "LandValue",
                schema: "collateral",
                table: "CollateralEngagements");
        }
    }
}
