using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingAgeAndFloorsToEngagementBuilding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuildingAge",
                schema: "collateral",
                table: "CollateralEngagementBuildings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NumberOfFloors",
                schema: "collateral",
                table: "CollateralEngagementBuildings",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingAge",
                schema: "collateral",
                table: "CollateralEngagementBuildings");

            migrationBuilder.DropColumn(
                name: "NumberOfFloors",
                schema: "collateral",
                table: "CollateralEngagementBuildings");
        }
    }
}
