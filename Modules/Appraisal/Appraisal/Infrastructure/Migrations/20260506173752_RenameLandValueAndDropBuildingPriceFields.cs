using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameLandValueAndDropBuildingPriceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old computed AppraisalPrice FIRST to free the column name before renaming AppraisalPriceWithBuildingRounded → AppraisalPrice.
            migrationBuilder.DropColumn(
                name: "AppraisalPrice",
                schema: "appraisal",
                table: "PricingFinalValues");

            migrationBuilder.DropColumn(
                name: "AppraisalPriceWithBuilding",
                schema: "appraisal",
                table: "PricingFinalValues");

            migrationBuilder.DropColumn(
                name: "PriceDifferentiate",
                schema: "appraisal",
                table: "PricingFinalValues");

            // AppraisalPriceRounded (user-edited land value) → LandValue
            migrationBuilder.RenameColumn(
                name: "AppraisalPriceRounded",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "LandValue");

            // AppraisalPriceWithBuildingRounded (user-edited final total) → AppraisalPrice
            migrationBuilder.RenameColumn(
                name: "AppraisalPriceWithBuildingRounded",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "AppraisalPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValueAdjusted",
                schema: "appraisal",
                table: "PricingFinalValues",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalValueAdjusted",
                schema: "appraisal",
                table: "PricingFinalValues");

            // Reverse rename AppraisalPrice → AppraisalPriceWithBuildingRounded BEFORE restoring old AppraisalPrice column.
            migrationBuilder.RenameColumn(
                name: "AppraisalPrice",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "AppraisalPriceWithBuildingRounded");

            migrationBuilder.RenameColumn(
                name: "LandValue",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "AppraisalPriceRounded");

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalPrice",
                schema: "appraisal",
                table: "PricingFinalValues",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalPriceWithBuilding",
                schema: "appraisal",
                table: "PricingFinalValues",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceDifferentiate",
                schema: "appraisal",
                table: "PricingFinalValues",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
