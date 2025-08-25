using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "collateral");

            migrationBuilder.CreateTable(
                name: "CollateralMasters",
                schema: "collateral",
                columns: table => new
                {
                    CollatId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatType = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    HostCollatId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralMasters", x => x.CollatId);
                });

            migrationBuilder.CreateTable(
                name: "CollateralBuildings",
                schema: "collateral",
                columns: table => new
                {
                    BuildingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    BuildingNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HouseNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BuiltOnTitleNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralBuildings", x => x.BuildingId);
                    table.ForeignKey(
                        name: "FK_CollateralBuildings_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollateralCondos",
                schema: "collateral",
                columns: table => new
                {
                    CondoId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    CondoName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BuildingNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BuiltOnTitleNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CondoRegisNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RoomNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FloorNo = table.Column<int>(type: "int", nullable: false),
                    UsableArea = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SubDistrict = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    District = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LandOffice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralCondos", x => x.CondoId);
                    table.ForeignKey(
                        name: "FK_CollateralCondos_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollateralLands",
                schema: "collateral",
                columns: table => new
                {
                    LandId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    SubDistrict = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    District = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LandOffice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LandDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralLands", x => x.LandId);
                    table.ForeignKey(
                        name: "FK_CollateralLands_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollateralMachines",
                schema: "collateral",
                columns: table => new
                {
                    MachineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnergyUse = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    CountryOfManufacture = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", maxLength: 30, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Capacity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    ChassisNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralMachines", x => x.MachineId);
                    table.ForeignKey(
                        name: "FK_CollateralMachines_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollateralVehicles",
                schema: "collateral",
                columns: table => new
                {
                    VehicleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnergyUse = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    CountryOfManufacture = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", maxLength: 30, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Capacity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    ChassisNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralVehicles", x => x.VehicleId);
                    table.ForeignKey(
                        name: "FK_CollateralVehicles_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollateralVessels",
                schema: "collateral",
                columns: table => new
                {
                    VesselId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    ApprId = table.Column<long>(type: "bigint", nullable: false),
                    VesselName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnergyUse = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EngineNo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    YearOfManufacture = table.Column<int>(type: "int", nullable: true),
                    CountryOfManufacture = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", maxLength: 30, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Capacity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralVessels", x => x.VesselId);
                    table.ForeignKey(
                        name: "FK_CollateralVessels_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandTitles",
                schema: "collateral",
                columns: table => new
                {
                    LandTitleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollatId = table.Column<long>(type: "bigint", nullable: false),
                    SeqNo = table.Column<int>(type: "int", nullable: false),
                    TitleNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BookNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PageNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LandNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SurveyNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SheetNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Rai = table.Column<decimal>(type: "decimal(3,0)", precision: 3, scale: 0, nullable: true),
                    Ngan = table.Column<decimal>(type: "decimal(5,0)", precision: 5, scale: 0, nullable: true),
                    Wa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    TotalAreaInSqWa = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    DocumentType = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    Rawang = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AerialPhotoNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BoundaryMarker = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    BoundaryMarkerOther = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocValidate = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    PricePerSquareWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    GovernmentPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandTitles", x => x.LandTitleId);
                    table.ForeignKey(
                        name: "FK_LandTitles_CollateralMasters_CollatId",
                        column: x => x.CollatId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "CollatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralBuildings_CollatId",
                schema: "collateral",
                table: "CollateralBuildings",
                column: "CollatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralCondos_CollatId",
                schema: "collateral",
                table: "CollateralCondos",
                column: "CollatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralLands_CollatId",
                schema: "collateral",
                table: "CollateralLands",
                column: "CollatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralMachines_CollatId",
                schema: "collateral",
                table: "CollateralMachines",
                column: "CollatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralVehicles_CollatId",
                schema: "collateral",
                table: "CollateralVehicles",
                column: "CollatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralVessels_CollatId",
                schema: "collateral",
                table: "CollateralVessels",
                column: "CollatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandTitles_CollatId",
                schema: "collateral",
                table: "LandTitles",
                column: "CollatId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollateralBuildings",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralCondos",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralLands",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralMachines",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralVehicles",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralVessels",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "LandTitles",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "CollateralMasters",
                schema: "collateral");
        }
    }
}
