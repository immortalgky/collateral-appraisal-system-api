using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketComparablePriceAndSaleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OfferPrice",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OfferPriceAdjustmentAmount",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OfferPriceAdjustmentPercent",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaleDate",
                schema: "appraisal",
                table: "MarketComparables",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfferPrice",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "OfferPriceAdjustmentAmount",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "OfferPriceAdjustmentPercent",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "SaleDate",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                schema: "appraisal",
                table: "MarketComparables");
        }
    }
}
