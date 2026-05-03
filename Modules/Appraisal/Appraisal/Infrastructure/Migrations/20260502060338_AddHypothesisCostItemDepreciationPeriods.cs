using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHypothesisCostItemDepreciationPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepreciationMethod",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Gross");

            migrationBuilder.AddColumn<bool>(
                name: "IsBuilding",
                schema: "appraisal",
                table: "HypothesisCostItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "HypothesisCostItemDepreciationPeriods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CostItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AtYear = table.Column<int>(type: "int", nullable: false),
                    ToYear = table.Column<int>(type: "int", nullable: false),
                    DepreciationPerYear = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HypothesisCostItemDepreciationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HypothesisCostItemDepreciationPeriods_HypothesisCostItems_CostItemId",
                        column: x => x.CostItemId,
                        principalSchema: "appraisal",
                        principalTable: "HypothesisCostItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HypothesisCostItemDepreciationPeriods_CostItemId",
                schema: "appraisal",
                table: "HypothesisCostItemDepreciationPeriods",
                column: "CostItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HypothesisCostItemDepreciationPeriods",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "DepreciationMethod",
                schema: "appraisal",
                table: "HypothesisCostItems");

            migrationBuilder.DropColumn(
                name: "IsBuilding",
                schema: "appraisal",
                table: "HypothesisCostItems");
        }
    }
}
