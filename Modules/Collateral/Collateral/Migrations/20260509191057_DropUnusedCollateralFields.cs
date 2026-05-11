using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedCollateralFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChassisNo",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "EngineNo",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "MachineAge",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "MachineCondition",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "YearOfManufacture",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "AnnualRent",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "LeasePurpose",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "LastTotalAppraisedValue",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "CondoDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChassisNo",
                schema: "collateral",
                table: "MachineDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineNo",
                schema: "collateral",
                table: "MachineDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "MachineDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MachineAge",
                schema: "collateral",
                table: "MachineDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineCondition",
                schema: "collateral",
                table: "MachineDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfManufacture",
                schema: "collateral",
                table: "MachineDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualRent",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeasePurpose",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastTotalAppraisedValue",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastAppraisedValue",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
