using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalGroupTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupTag",
                schema: "appraisal",
                table: "Appraisals",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_GroupTag",
                schema: "appraisal",
                table: "Appraisals",
                column: "GroupTag",
                filter: "[GroupTag] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appraisals_GroupTag",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "GroupTag",
                schema: "appraisal",
                table: "Appraisals");
        }
    }
}
