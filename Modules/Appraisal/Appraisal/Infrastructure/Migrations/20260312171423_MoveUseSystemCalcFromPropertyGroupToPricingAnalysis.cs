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

            // 2. Copy existing values from PropertyGroups → PricingAnalysis
            migrationBuilder.Sql("""
                UPDATE pa
                SET pa.UseSystemCalc = pg.UseSystemCalc
                FROM appraisal.PricingAnalysis pa
                INNER JOIN appraisal.PropertyGroups pg ON pg.Id = pa.PropertyGroupId
                """);

            // 3. Drop old column
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
