using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTowerConstructionYearToBuildingAgeAndDropTotalFloors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalNumberOfFloors",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.RenameColumn(
                name: "ConstructionYear",
                schema: "appraisal",
                table: "ProjectTowers",
                newName: "BuildingAge");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuildingAge",
                schema: "appraisal",
                table: "ProjectTowers",
                newName: "ConstructionYear");

            migrationBuilder.AddColumn<int>(
                name: "TotalNumberOfFloors",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "int",
                nullable: true);
        }
    }
}
