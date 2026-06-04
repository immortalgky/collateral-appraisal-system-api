using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCollateralAndReappraisalFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromReappraisal",
                schema: "collateral",
                table: "CollateralMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExcludedFromReappraisalAt",
                schema: "collateral",
                table: "CollateralMasters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedFromReappraisalBy",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectDetails",
                schema: "collateral",
                columns: table => new
                {
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Developer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    RemainingUnits = table.Column<int>(type: "int", nullable: false),
                    ProjectSellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StructureJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastAppraisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDetails", x => x.CollateralMasterId);
                    table.ForeignKey(
                        name: "FK_ProjectDetails_CollateralMasters_CollateralMasterId",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDetails",
                schema: "collateral");

            migrationBuilder.DropColumn(
                name: "ExcludedFromReappraisal",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "ExcludedFromReappraisalAt",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.DropColumn(
                name: "ExcludedFromReappraisalBy",
                schema: "collateral",
                table: "CollateralMasters");
        }
    }
}
