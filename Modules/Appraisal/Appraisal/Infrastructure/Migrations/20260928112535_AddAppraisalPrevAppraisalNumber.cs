using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalPrevAppraisalNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrevAppraisalNumber",
                schema: "appraisal",
                table: "Appraisals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_PrevAppraisalNumber",
                schema: "appraisal",
                table: "Appraisals",
                column: "PrevAppraisalNumber",
                filter: "[PrevAppraisalNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appraisals_PrevAppraisalNumber",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "PrevAppraisalNumber",
                schema: "appraisal",
                table: "Appraisals");
        }
    }
}
