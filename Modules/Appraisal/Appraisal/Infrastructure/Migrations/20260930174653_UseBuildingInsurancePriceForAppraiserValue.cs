using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseBuildingInsurancePriceForAppraiserValue : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Hand-edited: the appraiser's value already lives in BuildingInsurancePriceOverride, so the legacy
        /// column is dropped and the override is renamed into its place. Scaffolded as a plain drop of the
        /// override, which would have lost every keyed value.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingInsurancePrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.RenameColumn(
                name: "BuildingInsurancePriceOverride",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingInsurancePrice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuildingInsurancePrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingInsurancePriceOverride");

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingInsurancePrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
