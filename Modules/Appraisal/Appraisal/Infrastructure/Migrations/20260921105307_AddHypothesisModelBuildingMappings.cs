using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHypothesisModelBuildingMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HypothesisModelBuildingMappings",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    HypothesisAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisModelBuildingMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisModelBuildingMappings_HypothesisAnalyses_HypothesisAnalysisId",
                        column: x => x.HypothesisAnalysisId,
                        principalSchema: "appraisal",
                        principalTable: "HypothesisAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisModelBuildingMappings_HypothesisAnalysisId_ModelName",
                schema: "appraisal",
                table: "HypothesisModelBuildingMappings",
                columns: new[] { "HypothesisAnalysisId", "ModelName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HypothesisModelBuildingMappings",
                schema: "appraisal");
        }
    }
}
