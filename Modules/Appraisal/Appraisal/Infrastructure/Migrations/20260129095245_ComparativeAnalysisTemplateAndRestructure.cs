using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComparativeAnalysisTemplateAndRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PricingFactorScores_PricingCalculations_PricingCalculationId",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropIndex(
                name: "IX_PricingFactorScores_PricingCalculationId_FactorId",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "ComparableScore",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "ComparableValue",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "ScoreDifference",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.RenameColumn(
                name: "SubjectValue",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "SubjectScore",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "Score");

            migrationBuilder.RenameColumn(
                name: "PricingCalculationId",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "PricingMethodId");

            migrationBuilder.RenameIndex(
                name: "IX_PricingFactorScores_PricingCalculationId",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "IX_PricingFactorScores_PricingMethodId");

            migrationBuilder.AddColumn<Guid>(
                name: "MarketComparableId",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComparativeAnalysisTemplates",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparativeAnalysisTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingComparativeFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsSelectedForScoring = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingComparativeFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingComparativeFactors_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComparativeAnalysisTemplateFactors",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DefaultWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparativeAnalysisTemplateFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComparativeAnalysisTemplateFactors_ComparativeAnalysisTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "appraisal",
                        principalTable: "ComparativeAnalysisTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingFactorScores_PricingMethodId_MarketComparableId_FactorId",
                schema: "appraisal",
                table: "PricingFactorScores",
                columns: new[] { "PricingMethodId", "MarketComparableId", "FactorId" },
                unique: true,
                filter: "[MarketComparableId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplateFactors_TemplateId",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplateFactors",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplateFactors_TemplateId_FactorId",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplateFactors",
                columns: new[] { "TemplateId", "FactorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplates_PropertyType",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplates",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_ComparativeAnalysisTemplates_TemplateCode",
                schema: "appraisal",
                table: "ComparativeAnalysisTemplates",
                column: "TemplateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingComparativeFactors_PricingMethodId",
                schema: "appraisal",
                table: "PricingComparativeFactors",
                column: "PricingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingComparativeFactors_PricingMethodId_FactorId",
                schema: "appraisal",
                table: "PricingComparativeFactors",
                columns: new[] { "PricingMethodId", "FactorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PricingFactorScores_PricingAnalysisMethods_PricingMethodId",
                schema: "appraisal",
                table: "PricingFactorScores",
                column: "PricingMethodId",
                principalSchema: "appraisal",
                principalTable: "PricingAnalysisMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PricingFactorScores_PricingAnalysisMethods_PricingMethodId",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropTable(
                name: "ComparativeAnalysisTemplateFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PricingComparativeFactors",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ComparativeAnalysisTemplates",
                schema: "appraisal");

            migrationBuilder.DropIndex(
                name: "IX_PricingFactorScores_PricingMethodId_MarketComparableId_FactorId",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.DropColumn(
                name: "MarketComparableId",
                schema: "appraisal",
                table: "PricingFactorScores");

            migrationBuilder.RenameColumn(
                name: "Value",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "SubjectValue");

            migrationBuilder.RenameColumn(
                name: "Score",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "SubjectScore");

            migrationBuilder.RenameColumn(
                name: "PricingMethodId",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "PricingCalculationId");

            migrationBuilder.RenameIndex(
                name: "IX_PricingFactorScores_PricingMethodId",
                schema: "appraisal",
                table: "PricingFactorScores",
                newName: "IX_PricingFactorScores_PricingCalculationId");

            migrationBuilder.AddColumn<decimal>(
                name: "ComparableScore",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparableValue",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScoreDifference",
                schema: "appraisal",
                table: "PricingFactorScores",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingFactorScores_PricingCalculationId_FactorId",
                schema: "appraisal",
                table: "PricingFactorScores",
                columns: new[] { "PricingCalculationId", "FactorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PricingFactorScores_PricingCalculations_PricingCalculationId",
                schema: "appraisal",
                table: "PricingFactorScores",
                column: "PricingCalculationId",
                principalSchema: "appraisal",
                principalTable: "PricingCalculations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
