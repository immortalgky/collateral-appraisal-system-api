using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceUnitFieldsToMarketComparable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfferPriceUnit",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalePriceUnit",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfferPriceUnit",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "SalePriceUnit",
                schema: "appraisal",
                table: "MarketComparables");
        }
    }
}
