using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineCostItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineCostItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PricingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    RcnReplacementCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LifeSpanYears = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    ConditionFactor = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FunctionalObsolescence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EconomicObsolescence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FairMarketValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MarketDemandAvailable = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineCostItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineCostItems_PricingAnalysisMethods_PricingMethodId",
                        column: x => x.PricingMethodId,
                        principalSchema: "appraisal",
                        principalTable: "PricingAnalysisMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineCostItems_PricingMethodId",
                schema: "appraisal",
                table: "MachineCostItems",
                column: "PricingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineCostItems_PricingMethodId_AppraisalPropertyId",
                schema: "appraisal",
                table: "MachineCostItems",
                columns: new[] { "PricingMethodId", "AppraisalPropertyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineCostItems",
                schema: "appraisal");
        }
    }
}
