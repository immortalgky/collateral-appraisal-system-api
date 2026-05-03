using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHypothesisCostItemBuildingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AnnualDepreciationPercent",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Area",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepreciationAmount",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(17,2)",
                precision: 17,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceBeforeDepreciation",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(17,2)",
                precision: 17,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerSqM",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(17,2)",
                precision: 17,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDepreciationPercent",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValueAfterDepreciation",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "decimal(17,2)",
                precision: 17,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualDepreciationPercent",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "Area",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "DepreciationAmount",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "PriceBeforeDepreciation",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "PricePerSqM",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "TotalDepreciationPercent",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "ValueAfterDepreciation",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "Year",
                schema: "appraisal",
                table: "HypothesisCostItems");
        }
    }
}
