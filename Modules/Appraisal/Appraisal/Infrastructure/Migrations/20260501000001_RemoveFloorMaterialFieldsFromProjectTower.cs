using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFloorMaterialFieldsFromProjectTower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BathroomFloorMaterialType",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "BathroomFloorMaterialTypeOther",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "GroundFloorMaterialType",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "GroundFloorMaterialTypeOther",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "UpperFloorMaterialType",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "UpperFloorMaterialTypeOther",
                schema: "appraisal",
                table: "ProjectTowers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BathroomFloorMaterialType",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BathroomFloorMaterialTypeOther",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroundFloorMaterialType",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroundFloorMaterialTypeOther",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpperFloorMaterialType",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpperFloorMaterialTypeOther",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }
    }
}
