using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakePropertyGroupNumberIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyGroups_AppraisalId_GroupNumber",
                schema: "appraisal",
                table: "PropertyGroups");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_AppraisalId_GroupNumber",
                schema: "appraisal",
                table: "PropertyGroups",
                columns: new[] { "AppraisalId", "GroupNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyGroups_AppraisalId_GroupNumber",
                schema: "appraisal",
                table: "PropertyGroups");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_AppraisalId_GroupNumber",
                schema: "appraisal",
                table: "PropertyGroups",
                columns: new[] { "AppraisalId", "GroupNumber" },
                unique: true);
        }
    }
}
