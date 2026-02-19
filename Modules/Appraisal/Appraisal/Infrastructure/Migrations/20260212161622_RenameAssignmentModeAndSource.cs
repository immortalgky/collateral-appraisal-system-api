using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAssignmentModeAndSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AssignmentMode",
                schema: "appraisal",
                table: "AppraisalAssignments",
                newName: "AssignmentType");

            migrationBuilder.RenameColumn(
                name: "AssignmentSource",
                schema: "appraisal",
                table: "AppraisalAssignments",
                newName: "AssignmentMethod");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AssignmentType",
                schema: "appraisal",
                table: "AppraisalAssignments",
                newName: "AssignmentMode");

            migrationBuilder.RenameColumn(
                name: "AssignmentMethod",
                schema: "appraisal",
                table: "AppraisalAssignments",
                newName: "AssignmentSource");
        }
    }
}
