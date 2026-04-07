using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfitRentAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfitRentAnalyses",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketRentalFeePerSqWa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrowthRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrowthRatePercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    GrowthIntervalYears = table.Column<int>(type: "int", nullable: false),
                    DiscountRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IncludeBuildingCost = table.Column<bool>(type: "bit", nullable: false),
                    EstimatePriceRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalMarketRentalFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalContractRentalFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalReturnsFromLease = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPresentValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalValueRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitRentAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfitRentAnalyses_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfitRentCalculationDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ProfitRentAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    NumberOfMonths = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MarketRentalFeePerSqWa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarketRentalFeeGrowthPercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    MarketRentalFeePerMonth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarketRentalFeePerYear = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContractRentalFeePerYear = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnsFromLease = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PvFactor = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    PresentValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitRentCalculationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfitRentCalculationDetails_ProfitRentAnalyses_ProfitRentAnalysisId",
                        column: x => x.ProfitRentAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "ProfitRentAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfitRentGrowthPeriods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ProfitRentAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromYear = table.Column<int>(type: "int", nullable: false),
                    ToYear = table.Column<int>(type: "int", nullable: false),
                    GrowthRatePercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitRentGrowthPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfitRentGrowthPeriods_ProfitRentAnalyses_ProfitRentAnalysisId",
                        column: x => x.ProfitRentAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "ProfitRentAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfitRentAnalyses_PricingMethodId",
                schema: "appraisal",
                table: "ProfitRentAnalyses",
                column: "PricingMethodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfitRentCalculationDetails_ProfitRentAnalysisId",
                schema: "appraisal",
                table: "ProfitRentCalculationDetails",
                column: "ProfitRentAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitRentGrowthPeriods_ProfitRentAnalysisId",
                schema: "appraisal",
                table: "ProfitRentGrowthPeriods",
                column: "ProfitRentAnalysisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfitRentCalculationDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProfitRentGrowthPeriods",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProfitRentAnalyses",
                schema: "appraisal");
        }
    }
}
