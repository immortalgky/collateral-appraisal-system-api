using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitNumber",
                schema: "collateral",
                table: "ProjectUnits",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_UnitNumber",
                schema: "collateral",
                table: "ProjectUnits",
                column: "UnitNumber",
                filter: "[UnitNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_UnitNumber",
                schema: "collateral",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                schema: "collateral",
                table: "ProjectUnits");
        }
    }
}
