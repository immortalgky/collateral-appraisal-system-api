using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHypothesisUnitRowFsdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FloorNo",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark1",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark2",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UsableAreaSqM",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Apartment",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark1",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark2",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorNo",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows");

            migrationBuilder.DropColumn(
                name: "Remark1",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows");

            migrationBuilder.DropColumn(
                name: "Remark2",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows");

            migrationBuilder.DropColumn(
                name: "UsableAreaSqM",
                schema: "appraisal",
                table: "HypothesisLandBuildingUnitRows");

            migrationBuilder.DropColumn(
                name: "Apartment",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows");

            migrationBuilder.DropColumn(
                name: "Remark1",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows");

            migrationBuilder.DropColumn(
                name: "Remark2",
                schema: "appraisal",
                table: "HypothesisCondominiumUnitRows");
        }
    }
}
