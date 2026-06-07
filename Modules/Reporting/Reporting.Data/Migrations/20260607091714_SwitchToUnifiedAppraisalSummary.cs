using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToUnifiedAppraisalSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-summary-block");

            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-summary-condo");

            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-summary-construction");

            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-summary-land-building");

            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-summary-machine");

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[] { "appraisal-summary", "AppraisalSummary", "Appraisal Summary", "สรุปรายงานการประเมิน", "Async", true, "appraisal-summary", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-summary");

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[,]
                {
                    { "appraisal-summary-block", "AppraisalSummary", "Appraisal Summary – Block Project", "ใบสรุปรายงานการประเมิน – โครงการ (Block)", "Async", true, "appraisal-summary-block", 1 },
                    { "appraisal-summary-condo", "AppraisalSummary", "Appraisal Summary – Condominium", "ใบสรุปรายงานการประเมิน – ห้องชุด (Condo)", "Async", true, "appraisal-summary-condo", 1 },
                    { "appraisal-summary-construction", "AppraisalSummary", "Appraisal Summary – Construction Inspection", "สรุปรายงานการตรวจงานก่อสร้าง", "Async", true, "appraisal-summary-construction", 1 },
                    { "appraisal-summary-land-building", "AppraisalSummary", "Appraisal Summary – Land and Building", "ใบสรุปรายงานการประเมิน – ที่ดินและสิ่งปลูกสร้าง", "Async", true, "appraisal-summary-land-building", 1 },
                    { "appraisal-summary-machine", "AppraisalSummary", "Appraisal Summary – Machinery", "ใบสรุปรายงานการประเมิน – เครื่องจักร", "Async", true, "appraisal-summary-machine", 1 }
                });
        }
    }
}
