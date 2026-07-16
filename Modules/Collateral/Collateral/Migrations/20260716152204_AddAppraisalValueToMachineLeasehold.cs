using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalValueToMachineLeasehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "MachineDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LeaseholdDetails",
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
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LeaseholdDetails");
        }
    }
}
