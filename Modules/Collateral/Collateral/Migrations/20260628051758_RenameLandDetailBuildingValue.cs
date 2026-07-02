using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class RenameLandDetailBuildingValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuildingCost",
                schema: "collateral",
                table: "LandDetails",
                newName: "BuildingValue");

            migrationBuilder.RenameColumn(
                name: "BuildingCost",
                schema: "collateral",
                table: "CondoDetails",
                newName: "BuildingValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuildingValue",
                schema: "collateral",
                table: "LandDetails",
                newName: "BuildingCost");

            migrationBuilder.RenameColumn(
                name: "BuildingValue",
                schema: "collateral",
                table: "CondoDetails",
                newName: "BuildingCost");
        }
    }
}
