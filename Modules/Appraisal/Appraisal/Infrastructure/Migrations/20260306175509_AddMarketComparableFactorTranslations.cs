using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketComparableFactorTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create translation table first
            migrationBuilder.CreateTable(
                name: "MarketComparableFactorTranslations",
                schema: "appraisal",
                columns: table => new
                {
                    MarketComparableFactorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FactorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketComparableFactorTranslations", x => new { x.MarketComparableFactorId, x.Language });
                    table.ForeignKey(
                        name: "FK_MarketComparableFactorTranslations_MarketComparableFactors_MarketComparableFactorId",
                        column: x => x.MarketComparableFactorId,
                        principalSchema: "appraisal",
                        principalTable: "MarketComparableFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            // 2. Drop the old column. The copy into the translations table was removed;
            // only safe on an empty table.
            migrationBuilder.DropColumn(
                name: "FactorName",
                schema: "appraisal",
                table: "MarketComparableFactors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketComparableFactorTranslations",
                schema: "appraisal");

            migrationBuilder.AddColumn<string>(
                name: "FactorName",
                schema: "appraisal",
                table: "MarketComparableFactors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
