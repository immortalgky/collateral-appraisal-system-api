using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationSummaryReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[] { "quotation-summary", "Quotation", "Quotation Summary", "สรุปรายการเล่มประเมิน", "Sync", true, "quotation-summary", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "quotation-summary");
        }
    }
}
