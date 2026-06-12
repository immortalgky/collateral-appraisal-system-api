using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Reporting.Data.Migrations
{
    /// <summary>
    /// Replaces the three separate appraisal-book reports
    /// (external-appraisal-report / internal-report-construction / internal-report-block) with one
    /// unified <c>appraisal-book</c> definition. The provider auto-detects internal vs external
    /// (AppraisalAssignments.AssignmentType) and the body type (standard / construction / block).
    /// </summary>
    public partial class AddAppraisalBookReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "external-appraisal-report");

            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "internal-report-construction");

            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "internal-report-block");

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[] { "appraisal-book", "AppraisalBook", "Appraisal Book", "เล่มรายงานการประเมิน", "Async", true, "appraisal-book", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appraisal-book");

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[,]
                {
                    { "external-appraisal-report", "ExternalReport", "External Appraisal Report", "รายงานการประเมินราคา (บริษัทภายนอก)", "Async", true, "external-appraisal-report", 1 },
                    { "internal-report-construction", "InternalReport", "Internal Construction Inspection Report", "รายงานตรวจงานก่อสร้าง (ภายใน)", "Async", true, "internal-report-construction", 1 },
                    { "internal-report-block", "InternalReport", "Internal Block Project Report", "รายงานโครงการ (ภายใน)", "Async", true, "internal-report-block", 1 }
                });
        }
    }
}
