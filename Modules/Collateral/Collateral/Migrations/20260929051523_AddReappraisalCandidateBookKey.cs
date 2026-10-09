using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddReappraisalCandidateBookKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastSeenFileDate",
                schema: "collateral",
                table: "ReappraisalCandidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSurveyNumber",
                schema: "collateral",
                table: "ReappraisalCandidates",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_LastSeenFileDate",
                schema: "collateral",
                table: "ReappraisalCandidates",
                column: "LastSeenFileDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_NormalizedSurveyNumber_CollateralId",
                schema: "collateral",
                table: "ReappraisalCandidates",
                columns: new[] { "NormalizedSurveyNumber", "CollateralId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReappraisalCandidate_LastSeenFileDate",
                schema: "collateral",
                table: "ReappraisalCandidates");

            migrationBuilder.DropIndex(
                name: "IX_ReappraisalCandidate_NormalizedSurveyNumber_CollateralId",
                schema: "collateral",
                table: "ReappraisalCandidates");

            migrationBuilder.DropColumn(
                name: "LastSeenFileDate",
                schema: "collateral",
                table: "ReappraisalCandidates");

            migrationBuilder.DropColumn(
                name: "NormalizedSurveyNumber",
                schema: "collateral",
                table: "ReappraisalCandidates");
        }
    }
}
