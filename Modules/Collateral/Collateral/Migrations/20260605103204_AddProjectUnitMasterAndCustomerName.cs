using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitMasterAndCustomerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectUnits",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: true),
                    TowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    LandArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsSold = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PurchaseBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoanBankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastAppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnits_CollateralMasters",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "ProjectDetails",
                        principalColumn: "CollateralMasterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_CollateralMasterId",
                schema: "collateral",
                table: "ProjectUnits",
                column: "CollateralMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_Master_Sequence",
                schema: "collateral",
                table: "ProjectUnits",
                columns: new[] { "CollateralMasterId", "SequenceNumber" });

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            // Drop the legacy blob column. The copy of its units into ProjectUnits was removed;
            // only safe on an empty table.
            migrationBuilder.DropColumn(
                name: "StructureJson",
                schema: "collateral",
                table: "ProjectDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectUnits",
                schema: "collateral");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                schema: "collateral",
                table: "CollateralMasters");

            migrationBuilder.AddColumn<string>(
                name: "StructureJson",
                schema: "collateral",
                table: "ProjectDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
