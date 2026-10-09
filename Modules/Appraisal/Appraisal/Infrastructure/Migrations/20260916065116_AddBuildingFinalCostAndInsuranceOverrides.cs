using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingFinalCostAndInsuranceOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuildingInsurancePriceOverride",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingInsurancePriceOverride",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalCostValueOverride",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingInsurancePriceOverride",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingInsurancePriceOverride",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "FinalCostValueOverride",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");
        }
    }
}
