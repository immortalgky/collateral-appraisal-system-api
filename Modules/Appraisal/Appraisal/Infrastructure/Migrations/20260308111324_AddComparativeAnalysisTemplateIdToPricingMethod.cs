using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComparativeAnalysisTemplateIdToPricingMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ComparativeAnalysisTemplateId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysisMethods_ComparativeAnalysisTemplateId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                column: "ComparativeAnalysisTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_PricingAnalysisMethods_ComparativeAnalysisTemplates_ComparativeAnalysisTemplateId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                column: "ComparativeAnalysisTemplateId",
                principalSchema: "appraisal",
                principalTable: "ComparativeAnalysisTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PricingAnalysisMethods_ComparativeAnalysisTemplates_ComparativeAnalysisTemplateId",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropIndex(
                name: "IX_PricingAnalysisMethods_ComparativeAnalysisTemplateId",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropColumn(
                name: "ComparativeAnalysisTemplateId",
                schema: "appraisal",
                table: "PricingAnalysisMethods");
        }
    }
}
