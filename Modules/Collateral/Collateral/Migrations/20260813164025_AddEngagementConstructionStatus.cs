using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementConstructionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConstructionProgressPercent",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderConstruction",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagements_UnderConstruction",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "IsUnderConstruction",
                filter: "[IsUnderConstruction] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollateralEngagements_UnderConstruction",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "ConstructionProgressPercent",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "IsUnderConstruction",
                schema: "collateral",
                table: "CollateralEngagements");
        }
    }
}
