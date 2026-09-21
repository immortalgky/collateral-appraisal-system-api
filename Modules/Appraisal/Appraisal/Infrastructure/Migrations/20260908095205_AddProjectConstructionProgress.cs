using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectConstructionProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConstructionProgressPercent",
                schema: "appraisal",
                table: "Projects",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderConstruction",
                schema: "appraisal",
                table: "Projects",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConstructionProgressPercent",
                schema: "appraisal",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsUnderConstruction",
                schema: "appraisal",
                table: "Projects");
        }
    }
}
