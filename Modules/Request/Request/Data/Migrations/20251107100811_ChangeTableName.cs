using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestDetauls",
                schema: "request");

            migrationBuilder.CreateTable(
                name: "RequestDetails",
                schema: "request",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasAppraisalBook = table.Column<bool>(type: "bit", nullable: false),
                    LoanApplicationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankingSegment = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    AdditionalFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PreviousFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TotalSellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalNo = table.Column<long>(type: "bigint", maxLength: 10, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LocationIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ContactPersonName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPersonPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentLocation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FeeType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FeeRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BankAbsorbAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestDetails",
                schema: "request");

            migrationBuilder.CreateTable(
                name: "RequestDetauls",
                schema: "request",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasAppraisalBook = table.Column<bool>(type: "bit", nullable: false),
                    PrevAppraisalNo = table.Column<long>(type: "bigint", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LocationIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentLocation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ContactPersonName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPersonPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BankAbsorbAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    FeeRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FeeType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AdditionalFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    BankingSegment = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    LoanApplicationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreviousFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TotalSellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDetauls", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_RequestDetauls_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
