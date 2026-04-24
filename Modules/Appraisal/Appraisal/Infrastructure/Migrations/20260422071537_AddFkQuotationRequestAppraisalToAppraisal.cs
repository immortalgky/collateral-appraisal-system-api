using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFkQuotationRequestAppraisalToAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_QuotationRequestAppraisals_Appraisals_AppraisalId",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                column: "AppraisalId",
                principalSchema: "appraisal",
                principalTable: "Appraisals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationRequestAppraisals_Appraisals_AppraisalId",
                schema: "appraisal",
                table: "QuotationRequestAppraisals");
        }
    }
}
