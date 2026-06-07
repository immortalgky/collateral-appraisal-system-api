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

            // --- Backfill: materialize existing ProjectDetails.StructureJson blobs into per-unit rows
            // BEFORE the column is dropped. Runs atomically inside this migration so the data survives.
            // Id omitted → NEWSEQUENTIALID() default. PurchaseBy/LoanBankName/LastAppraisedValue have no
            // source in the legacy JSON → left null. IsSold coalesced to 0 to satisfy the NOT NULL column.
            migrationBuilder.Sql(@"
INSERT INTO collateral.ProjectUnits
    (CollateralMasterId, SequenceNumber, Floor, TowerName, CondoRegistrationNumber, RoomNumber,
     PlotNumber, HouseNumber, NumberOfFloors, LandArea, ModelType, UsableArea, SellingPrice, IsSold)
SELECT pd.CollateralMasterId,
       u.SequenceNumber, u.Floor, u.TowerName, u.CondoRegistrationNumber, u.RoomNumber,
       u.PlotNumber, u.HouseNumber, u.NumberOfFloors, u.LandArea, u.ModelType, u.UsableArea, u.SellingPrice,
       ISNULL(u.IsSold, 0)
FROM collateral.ProjectDetails pd
CROSS APPLY OPENJSON(pd.StructureJson, '$.Units')
WITH (
    SequenceNumber          int             '$.SequenceNumber',
    IsSold                  bit             '$.IsSold',
    ModelType               nvarchar(200)   '$.ModelType',
    UsableArea              decimal(10,2)   '$.UsableArea',
    SellingPrice            decimal(18,2)   '$.SellingPrice',
    Floor                   int             '$.Floor',
    TowerName               nvarchar(200)   '$.TowerName',
    CondoRegistrationNumber nvarchar(100)   '$.CondoRegistrationNumber',
    RoomNumber              nvarchar(50)    '$.RoomNumber',
    PlotNumber              nvarchar(100)   '$.PlotNumber',
    HouseNumber             nvarchar(100)   '$.HouseNumber',
    NumberOfFloors          int             '$.NumberOfFloors',
    LandArea                decimal(10,2)   '$.LandArea'
) u
WHERE pd.StructureJson IS NOT NULL AND ISJSON(pd.StructureJson) = 1;
");

            // Now safe to drop the legacy blob column.
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
