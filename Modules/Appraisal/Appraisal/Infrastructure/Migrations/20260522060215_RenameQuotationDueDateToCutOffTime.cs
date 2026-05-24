using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuotationDueDateToCutOffTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DueDate",
                schema: "appraisal",
                table: "QuotationRequests",
                newName: "CutOffTime");

            migrationBuilder.RenameIndex(
                name: "IX_QuotationRequests_Status_DueDate",
                schema: "appraisal",
                table: "QuotationRequests",
                newName: "IX_QuotationRequests_Status_CutOffTime");

            migrationBuilder.RenameIndex(
                name: "IX_QuotationRequests_DueDate",
                schema: "appraisal",
                table: "QuotationRequests",
                newName: "IX_QuotationRequests_CutOffTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CutOffTime",
                schema: "appraisal",
                table: "QuotationRequests",
                newName: "DueDate");

            migrationBuilder.RenameIndex(
                name: "IX_QuotationRequests_Status_CutOffTime",
                schema: "appraisal",
                table: "QuotationRequests",
                newName: "IX_QuotationRequests_Status_DueDate");

            migrationBuilder.RenameIndex(
                name: "IX_QuotationRequests_CutOffTime",
                schema: "appraisal",
                table: "QuotationRequests",
                newName: "IX_QuotationRequests_DueDate");
        }
    }
}
