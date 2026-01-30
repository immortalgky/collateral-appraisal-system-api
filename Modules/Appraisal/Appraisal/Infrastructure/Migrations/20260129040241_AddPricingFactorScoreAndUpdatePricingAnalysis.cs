using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingFactorScoreAndUpdatePricingAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AppraisalId",
                schema: "appraisal",
                table: "PricingAnalysis",
                newName: "PropertyGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_PricingAnalysis_AppraisalId",
                schema: "appraisal",
                table: "PricingAnalysis",
                newName: "IX_PricingAnalysis_PropertyGroupId");

            migrationBuilder.CreateTable(
                name: "PricingFactorScores",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingCalculationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubjectScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ComparableValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ComparableScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ScoreDifference = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    FactorWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    WeightedScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AdjustmentPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingFactorScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingFactorScores_PricingCalculations_PricingCalculationId",
                        column: x => x.PricingCalculationId,
                        principalSchema: "appraisal",
                        principalTable: "PricingCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingFactorScores_PricingCalculationId",
                schema: "appraisal",
                table: "PricingFactorScores",
                column: "PricingCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingFactorScores_PricingCalculationId_FactorId",
                schema: "appraisal",
                table: "PricingFactorScores",
                columns: new[] { "PricingCalculationId", "FactorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingFactorScores",
                schema: "appraisal");

            migrationBuilder.RenameColumn(
                name: "PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                newName: "AppraisalId");

            migrationBuilder.RenameIndex(
                name: "IX_PricingAnalysis_PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                newName: "IX_PricingAnalysis_AppraisalId");
        }
    }
}
