using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveUseSystemCalcFromPropertyGroupToPricingAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new column with default
            migrationBuilder.AddColumn<bool>(
                name: "UseSystemCalc",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            // 2. Drop old column. The copy of existing values into PricingAnalysis was removed;
            // only safe on an empty table.
            migrationBuilder.DropColumn(
                name: "UseSystemCalc",
                schema: "appraisal",
                table: "PropertyGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseSystemCalc",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.AddColumn<bool>(
                name: "UseSystemCalc",
                schema: "appraisal",
                table: "PropertyGroups",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }
    }
}
