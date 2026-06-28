using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropPerMethodFinalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "ProfitRentAnalyses");

            migrationBuilder.DropColumn(
                name: "FinalValue",
                schema: "appraisal",
                table: "LeaseholdAnalyses");

            migrationBuilder.DropColumn(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "LeaseholdAnalyses");

            migrationBuilder.DropColumn(
                name: "AppraisalPriceRounded",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "FinalValue",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "FinalValueAdjust",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "IncomeAnalyses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "ProfitRentAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValue",
                schema: "appraisal",
                table: "LeaseholdAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "LeaseholdAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalPriceRounded",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValue",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValueAdjust",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
