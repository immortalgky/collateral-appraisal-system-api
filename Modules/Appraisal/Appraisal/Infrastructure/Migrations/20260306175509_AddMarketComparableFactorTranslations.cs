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

            // 2. Migrate existing FactorName data as English translations
            migrationBuilder.Sql("""
                INSERT INTO [appraisal].[MarketComparableFactorTranslations] ([MarketComparableFactorId], [Language], [FactorName])
                SELECT [Id], 'en', [FactorName]
                FROM [appraisal].[MarketComparableFactors]
                WHERE [FactorName] IS NOT NULL AND [FactorName] <> ''
                """);

            // 3. Drop the old column
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
