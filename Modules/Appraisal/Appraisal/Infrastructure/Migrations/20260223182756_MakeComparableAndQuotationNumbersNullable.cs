using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeComparableAndQuotationNumbersNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuotationRequests_QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.AlterColumn<string>(
                name: "QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "QuotationNumber",
                unique: true,
                filter: "[QuotationNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                column: "ComparableNumber",
                unique: true,
                filter: "[ComparableNumber] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuotationRequests_QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.AlterColumn<string>(
                name: "QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_QuotationNumber",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                column: "ComparableNumber",
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
