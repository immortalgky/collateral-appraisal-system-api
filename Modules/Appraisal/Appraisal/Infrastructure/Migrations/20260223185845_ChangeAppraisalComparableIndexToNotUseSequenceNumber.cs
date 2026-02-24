using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAppraisalComparableIndexToNotUseSequenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppraisalComparables_AppraisalId_SequenceNumber",
                schema: "appraisal",
                table: "AppraisalComparables");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalComparables_AppraisalId_MarketComparableId",
                schema: "appraisal",
                table: "AppraisalComparables",
                columns: new[] { "AppraisalId", "MarketComparableId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppraisalComparables_AppraisalId_MarketComparableId",
                schema: "appraisal",
                table: "AppraisalComparables");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalComparables_AppraisalId_SequenceNumber",
                schema: "appraisal",
                table: "AppraisalComparables",
                columns: new[] { "AppraisalId", "SequenceNumber" },
                unique: true);
        }
    }
}
