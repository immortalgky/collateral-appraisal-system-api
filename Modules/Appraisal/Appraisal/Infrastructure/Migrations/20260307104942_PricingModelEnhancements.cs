using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PricingModelEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmt",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonResult",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Intensity",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                schema: "appraisal",
                table: "PricingCalculations",
                type: "decimal(10,5)",
                precision: 10,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightedAdjustedValue",
                schema: "appraisal",
                table: "PricingCalculations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultIntensity",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplateFactors",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PricingRsqResults",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoefficientOfDecision = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    StandardError = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IntersectionPoint = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Slope = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RsqFinalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LowestEstimate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HighestEstimate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRsqResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingRsqResults_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRsqResults_PricingMethodId",
                schema: "appraisal",
                table: "PricingRsqResults",
                column: "PricingMethodId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingRsqResults",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmt",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "ComparisonResult",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "Intensity",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "appraisal",
                table: "PricingCalculations");

            migrationBuilder.DropColumn(
                name: "WeightedAdjustedValue",
                schema: "appraisal",
                table: "PricingCalculations");

            migrationBuilder.DropColumn(
                name: "DefaultIntensity",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplateFactors");
        }
    }
}
