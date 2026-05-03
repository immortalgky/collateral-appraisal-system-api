using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LocationNumber",
                schema: "appraisal",
                table: "Projects",
                newName: "HouseNumber");

            migrationBuilder.RenameColumn(
                name: "LandAreaWa",
                schema: "appraisal",
                table: "Projects",
                newName: "LandAreaSquareWa");

            migrationBuilder.RenameColumn(
                name: "LandAreaWa",
                schema: "appraisal",
                table: "ProjectModels",
                newName: "LandAreaSquareWa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LandAreaSquareWa",
                schema: "appraisal",
                table: "Projects",
                newName: "LandAreaWa");

            migrationBuilder.RenameColumn(
                name: "HouseNumber",
                schema: "appraisal",
                table: "Projects",
                newName: "LocationNumber");

            migrationBuilder.RenameColumn(
                name: "LandAreaSquareWa",
                schema: "appraisal",
                table: "ProjectModels",
                newName: "LandAreaWa");
        }
    }
}
