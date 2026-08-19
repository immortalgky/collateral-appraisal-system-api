using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitHostCollateralId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "ProjectUnits",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_HostCollateralId",
                schema: "collateral",
                table: "ProjectUnits",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_HostCollateralId",
                schema: "collateral",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "HostCollateralId",
                schema: "collateral",
                table: "ProjectUnits");
        }
    }
}
