using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncomeAnalyses",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingAnalysisMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalNumberOfYears = table.Column<int>(type: "int", nullable: false),
                    TotalNumberOfDayInYear = table.Column<int>(type: "int", nullable: false, defaultValue: 365),
                    CapitalizeRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountedRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FinalValueRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Summary_ContractRentalFeeJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Summary_GrossRevenueJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Summary_GrossRevenueProportionalJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Summary_TerminalRevenueJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Summary_TotalNetJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Summary_DiscountJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Summary_PresentValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeAnalyses_PricingAnalysisMethods_PricingAnalysisMethodId",
                        column: x => x.PricingAnalysisMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeSections",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    IncomeAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    TotalSectionValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeSections_IncomeAnalyses_IncomeAnalysisId",
                        column: x => x.IncomeAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "IncomeAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeCategories",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    IncomeSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    TotalCategoryValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeCategories_IncomeSections_IncomeSectionId",
                        column: x => x.IncomeSectionId,
                        principalSchema: "appraisal",
                        principalTable: "IncomeSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    IncomeCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssumptionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssumptionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false),
                    TotalAssumptionValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Method_MethodTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Method_DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    Method_TotalMethodValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeAssumptions_IncomeCategories_IncomeCategoryId",
                        column: x => x.IncomeCategoryId,
                        principalSchema: "appraisal",
                        principalTable: "IncomeCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeAnalyses_PricingAnalysisMethodId",
                schema: "appraisal",
                table: "IncomeAnalyses",
                column: "PricingAnalysisMethodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeAssumptions_IncomeCategoryId",
                schema: "appraisal",
                table: "IncomeAssumptions",
                column: "IncomeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeCategories_IncomeSectionId",
                schema: "appraisal",
                table: "IncomeCategories",
                column: "IncomeSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSections_IncomeAnalysisId",
                schema: "appraisal",
                table: "IncomeSections",
                column: "IncomeAnalysisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomeAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "IncomeCategories",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "IncomeSections",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "IncomeAnalyses",
                schema: "appraisal");
        }
    }
}
