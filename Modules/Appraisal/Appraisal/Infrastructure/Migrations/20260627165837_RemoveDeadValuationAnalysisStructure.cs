using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadValuationAnalysisStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupValuations",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "PropertyValuations",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "MarketValue",
                schema: "appraisal",
                table: "ValuationAnalyses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarketValue",
                schema: "appraisal",
                table: "ValuationAnalyses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "GroupValuations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForcedSaleValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValuationAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ValuationWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ValuePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupValuations_ValuationAnalyses_ValuationAnalysisId",
                        column: x => x.ValuationAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "ValuationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyValuations",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForcedSaleValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PropertyDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyDetailType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValuationAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ValuationWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ValuePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyValuations_ValuationAnalyses_ValuationAnalysisId",
                        column: x => x.ValuationAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "ValuationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupValuations_PropertyGroupId",
                schema: "appraisal",
                table: "GroupValuations",
                column: "PropertyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupValuations_ValuationAnalysisId",
                schema: "appraisal",
                table: "GroupValuations",
                column: "ValuationAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValuations_PropertyDetailType_PropertyDetailId",
                schema: "appraisal",
                table: "PropertyValuations",
                columns: new[] { "PropertyDetailType", "PropertyDetailId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValuations_ValuationAnalysisId",
                schema: "appraisal",
                table: "PropertyValuations",
                column: "ValuationAnalysisId");
        }
    }
}
