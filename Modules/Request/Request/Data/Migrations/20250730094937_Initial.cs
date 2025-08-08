using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "request");

            migrationBuilder.CreateTable(
                name: "RequestComments",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppraisalNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestTitles",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    CollateralType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TitleNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TitleDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Rai = table.Column<int>(type: "int", nullable: true),
                    Ngan = table.Column<int>(type: "int", nullable: true),
                    Wa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UsageArea = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    NoOfBuilding = table.Column<int>(type: "int", nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BuildingNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaHouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DopaRoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DopaFloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaBuildingNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaMoo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaSoi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaRoad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaSubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaProvince = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaPostcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleRegistrationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MachineStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MachineType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MachineRegistrationStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MachineRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MachineInvoiceNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestCustomers",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestCustomers_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestDetails",
                schema: "request",
                columns: table => new
                {
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HasAppraisalBook = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccurConstInspec = table.Column<int>(type: "int", nullable: true),
                    LoanApplicationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LimitAmt = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TotalSellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PrevAppraisalValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LocationIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ContactPersonName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContactPersonContactNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FeeType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FeeRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestorEmpId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestorEmail = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestorContactNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestorAo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorBranch = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorBusinessUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorDepartment = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorSection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorCostCenter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_RequestDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestProperties",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BuildingType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RequestId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestProperties_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestComments_RequestId",
                schema: "request",
                table: "RequestComments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomers_RequestId",
                schema: "request",
                table: "RequestCustomers",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestProperties_RequestId",
                schema: "request",
                table: "RequestProperties",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_AppraisalNo",
                schema: "request",
                table: "Requests",
                column: "AppraisalNo",
                unique: true,
                filter: "[AppraisalNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestComments",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestCustomers",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestDetails",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestProperties",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestTitles",
                schema: "request");

            migrationBuilder.DropTable(
                name: "Requests",
                schema: "request");
        }
    }
}
