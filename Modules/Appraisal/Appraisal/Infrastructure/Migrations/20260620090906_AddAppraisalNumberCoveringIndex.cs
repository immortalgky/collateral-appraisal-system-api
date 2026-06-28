using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalNumberCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appraisals_AppraisalNumber",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_AppraisalNumber",
                schema: "appraisal",
                table: "Appraisals",
                column: "AppraisalNumber",
                unique: true,
                filter: "[AppraisalNumber] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "RequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appraisals_AppraisalNumber",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_AppraisalNumber",
                schema: "appraisal",
                table: "Appraisals",
                column: "AppraisalNumber",
                unique: true,
                filter: "[AppraisalNumber] IS NOT NULL");
        }
    }
}
