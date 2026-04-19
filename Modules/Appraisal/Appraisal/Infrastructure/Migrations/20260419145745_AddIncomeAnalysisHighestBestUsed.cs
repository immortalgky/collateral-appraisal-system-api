using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeAnalysisHighestBestUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalPriceRounded",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HighestBestUsed_AreaNgan",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HighestBestUsed_AreaRai",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HighestBestUsed_AreaWa",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HighestBestUsed_PricePerSqWa",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHighestBestUsed",
                schema: "appraisal",
                table: "IncomeAnalyses",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalPriceRounded",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "HighestBestUsed_AreaNgan",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "HighestBestUsed_AreaRai",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "HighestBestUsed_AreaWa",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "HighestBestUsed_PricePerSqWa",
                schema: "appraisal",
                table: "IncomeAnalyses");

            migrationBuilder.DropColumn(
                name: "IsHighestBestUsed",
                schema: "appraisal",
                table: "IncomeAnalyses");
        }
    }
}
