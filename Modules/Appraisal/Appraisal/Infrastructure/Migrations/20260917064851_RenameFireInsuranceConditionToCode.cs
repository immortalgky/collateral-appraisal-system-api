using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameFireInsuranceConditionToCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FireInsuranceCondition",
                schema: "appraisal",
                table: "ProjectModels",
                newName: "FireInsuranceCode");

            migrationBuilder.RenameColumn(
                name: "FireInsuranceCondition",
                schema: "appraisal",
                table: "ProjectModelAssumptions",
                newName: "FireInsuranceCode");

            migrationBuilder.RenameColumn(
                name: "FireInsuranceCondition",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "FireInsuranceCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FireInsuranceCode",
                schema: "appraisal",
                table: "ProjectModels",
                newName: "FireInsuranceCondition");

            migrationBuilder.RenameColumn(
                name: "FireInsuranceCode",
                schema: "appraisal",
                table: "ProjectModelAssumptions",
                newName: "FireInsuranceCondition");

            migrationBuilder.RenameColumn(
                name: "FireInsuranceCode",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "FireInsuranceCondition");
        }
    }
}
