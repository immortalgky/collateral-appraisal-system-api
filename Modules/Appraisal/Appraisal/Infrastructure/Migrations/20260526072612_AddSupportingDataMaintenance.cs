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
                    SupportingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ImportChannel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceOfData = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AppraisalCompany = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Developer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CollateralType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BuildingType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LandArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoomFloor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    PlotLocationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    OfferingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InformationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
