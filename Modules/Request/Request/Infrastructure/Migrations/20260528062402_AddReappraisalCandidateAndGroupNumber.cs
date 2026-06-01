using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReappraisalCandidateAndGroupNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppraisalGroupNumber",
                schema: "request",
                table: "Requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReappraisalCandidates",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceFileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CollateralId = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    SurveyNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CollateralCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CollateralCategory = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CollateralName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CollateralAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CifNumber = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    CifName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AoCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AoName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TitleNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    ValuationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InternalExternal = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    BusinessSize = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    BusinessSizeDesc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MortgageAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    PastDueDay = table.Column<int>(type: "int", nullable: true),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true),
                    FacilityCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    FacilitySequence = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true),
                    CpNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CarCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    FlagLessAge4Y = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    FlagGreaterAge4Y = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    CountAgeingDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CollateralDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExternalValuerName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    InternalValuerName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SllOver100M = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    SllDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReappraisalCandidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Request_AppraisalGroupNumber",
                schema: "request",
                table: "Requests",
                column: "AppraisalGroupNumber",
                filter: "[AppraisalGroupNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_FileDate_CollateralId_SurveyNumber",
                schema: "request",
                table: "ReappraisalCandidates",
                columns: new[] { "SourceFileDate", "CollateralId", "SurveyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_ReviewDate",
                schema: "request",
                table: "ReappraisalCandidates",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_Status_Pending",
                schema: "request",
                table: "ReappraisalCandidates",
                column: "Status",
                filter: "[Status] = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReappraisalCandidates",
                schema: "request");

            migrationBuilder.DropIndex(
                name: "IX_Request_AppraisalGroupNumber",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AppraisalGroupNumber",
                schema: "request",
                table: "Requests");
        }
    }
}
