using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportingDataMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportingData",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupportingNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ImportChannel = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceOfData = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AppraisalCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportingData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportingDataDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupportingDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Developer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CollateralType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BuildingType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LandArea = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomFloor = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    PlotLocationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlotLocationTypeOther = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    OfferingPrice = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InformationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Website = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportingDataDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportingDataDetails_SupportingData_SupportingDataId",
                        column: x => x.SupportingDataId,
                        principalSchema: "appraisal",
                        principalTable: "SupportingData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportingData_ImportDate",
                schema: "appraisal",
                table: "SupportingData",
                column: "ImportDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SupportingData_Status",
                schema: "appraisal",
                table: "SupportingData",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupportingData_SupportingNumber",
                schema: "appraisal",
                table: "SupportingData",
                column: "SupportingNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupportingDataDetails_CollateralType",
                schema: "appraisal",
                table: "SupportingDataDetails",
                column: "CollateralType");

            migrationBuilder.CreateIndex(
                name: "IX_SupportingDataDetails_SupportingDataId",
                schema: "appraisal",
                table: "SupportingDataDetails",
                column: "SupportingDataId");


            // Persisted computed column — NULL-safe: only calls geography::Point() when both
            // Latitude and Longitude are non-NULL. PERSISTED requires the expression to be
            // deterministic; geography::Point with literal SRID 4326 satisfies that requirement.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.SupportingDataDetails
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            // Spatial index on the computed column (geography type uses default tessellation).
            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_SupportingDataDetails_GeoPoint
                    ON appraisal.SupportingDataDetails(GeoPoint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportingDataDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "SupportingData",
                schema: "appraisal");
        }
    }
}
