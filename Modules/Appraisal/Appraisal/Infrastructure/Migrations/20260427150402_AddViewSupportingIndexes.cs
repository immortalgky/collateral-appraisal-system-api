using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViewSupportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_IsDeleted_NotDeleted",
                schema: "appraisal",
                table: "Appraisals",
                column: "Id",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAssignments_AppraisalId_AssignedAt_Active",
                schema: "appraisal",
                table: "AppraisalAssignments",
                columns: new[] { "AppraisalId", "AssignedAt" },
                descending: new[] { false, true },
                filter: "[AssignmentStatus] <> 'Rejected' AND [AssignmentStatus] <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appraisals_IsDeleted_NotDeleted",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAssignments_AppraisalId_AssignedAt_Active",
                schema: "appraisal",
                table: "AppraisalAssignments");
        }
    }
}
