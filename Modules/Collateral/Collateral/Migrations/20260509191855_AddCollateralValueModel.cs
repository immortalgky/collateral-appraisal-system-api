using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddCollateralValueModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingCost",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingCost",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "BuildingCost",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "BuildingCost",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "collateral",
                table: "CondoDetails");
        }
    }
}
