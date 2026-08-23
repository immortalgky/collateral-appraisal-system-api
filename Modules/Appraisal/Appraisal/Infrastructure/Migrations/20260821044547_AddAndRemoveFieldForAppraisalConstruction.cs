using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAndRemoveFieldForAppraisalConstruction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConstructionCompletionPercent",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "ConstructionCompletionPercent",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderConstruction",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUnderConstruction",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.AddColumn<decimal>(
                name: "ConstructionCompletionPercent",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConstructionCompletionPercent",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }
    }
}
