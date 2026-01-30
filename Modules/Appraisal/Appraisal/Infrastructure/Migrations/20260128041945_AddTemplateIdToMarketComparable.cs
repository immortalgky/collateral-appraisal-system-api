using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateIdToMarketComparable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                schema: "appraisal",
                table: "MarketComparables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_TemplateId",
                schema: "appraisal",
                table: "MarketComparables",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_MarketComparables_MarketComparableTemplates_TemplateId",
                schema: "appraisal",
                table: "MarketComparables",
                column: "TemplateId",
                principalSchema: "appraisal",
                principalTable: "MarketComparableTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MarketComparables_MarketComparableTemplates_TemplateId",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_TemplateId",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                schema: "appraisal",
                table: "MarketComparables");
        }
    }
}
