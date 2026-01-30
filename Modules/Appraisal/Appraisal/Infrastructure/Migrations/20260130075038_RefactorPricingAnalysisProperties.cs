using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPricingAnalysisProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "appraisal",
                table: "PricingComparableLinks");

            migrationBuilder.DropColumn(
                name: "TotalInitialPrice",
                schema: "appraisal",
                table: "PricingCalculations");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "appraisal",
                table: "PricingCalculations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropColumn(
                name: "ExclusionReason",
                schema: "appraisal",
                table: "PricingAnalysisApproaches");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "appraisal",
                table: "PricingAnalysisApproaches");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "appraisal",
                table: "PricingAnalysisApproaches");

            migrationBuilder.DropColumn(
                name: "FinalForcedSaleValue",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "FinalMarketValue",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "ValuationDate",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                schema: "appraisal",
                table: "PricingAnalysisApproaches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSelected",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropColumn(
                name: "IsSelected",
                schema: "appraisal",
                table: "PricingAnalysisApproaches");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                schema: "appraisal",
                table: "PricingComparableLinks",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalInitialPrice",
                schema: "appraisal",
                table: "PricingCalculations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                schema: "appraisal",
                table: "PricingCalculations",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Selected");

            migrationBuilder.AddColumn<string>(
                name: "ExclusionReason",
                schema: "appraisal",
                table: "PricingAnalysisApproaches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "appraisal",
                table: "PricingAnalysisApproaches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                schema: "appraisal",
                table: "PricingAnalysisApproaches",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalForcedSaleValue",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalMarketValue",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValuationDate",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "datetime2",
                nullable: true);
        }
    }
}
