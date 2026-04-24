using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyQuotationFeeBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                schema: "appraisal",
                table: "CompanyQuotationItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                schema: "appraisal",
                table: "CompanyQuotationItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NegotiatedDiscount",
                schema: "appraisal",
                table: "CompanyQuotationItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                schema: "appraisal",
                table: "CompanyQuotationItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discount",
                schema: "appraisal",
                table: "CompanyQuotationItems");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                schema: "appraisal",
                table: "CompanyQuotationItems");

            migrationBuilder.DropColumn(
                name: "NegotiatedDiscount",
                schema: "appraisal",
                table: "CompanyQuotationItems");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                schema: "appraisal",
                table: "CompanyQuotationItems");
        }
    }
}
