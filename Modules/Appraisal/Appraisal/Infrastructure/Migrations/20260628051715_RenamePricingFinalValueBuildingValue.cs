using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePricingFinalValueBuildingValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HasBuildingCost",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "HasBuildingValue");

            migrationBuilder.RenameColumn(
                name: "BuildingCost",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "BuildingValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HasBuildingValue",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "HasBuildingCost");

            migrationBuilder.RenameColumn(
                name: "BuildingValue",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "BuildingCost");
        }
    }
}
