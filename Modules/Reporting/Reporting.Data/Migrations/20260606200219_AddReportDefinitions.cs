using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                schema: "reporting",
                columns: table => new
                {
                    ReportTypeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayNameTh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayNameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GenerationMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.ReportTypeKey);
                });

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[,]
                {
                    { "appointment-quotation-request", "Appointment", "Appointment Quotation Request", "แบบฟอร์มนัดหมายและใบเสนอราคา", "Sync", true, "appointment-quotation-request", 1 },
                    { "appraisal-summary-block", "AppraisalSummary", "Appraisal Summary – Block Project", "ใบสรุปรายงานการประเมิน – โครงการ (Block)", "Async", true, "appraisal-summary-block", 1 },
                    { "appraisal-summary-condo", "AppraisalSummary", "Appraisal Summary – Condominium", "ใบสรุปรายงานการประเมิน – ห้องชุด (Condo)", "Async", true, "appraisal-summary-condo", 1 },
                    { "appraisal-summary-construction", "AppraisalSummary", "Appraisal Summary – Construction Inspection", "สรุปรายงานการตรวจงานก่อสร้าง", "Async", true, "appraisal-summary-construction", 1 },
                    { "appraisal-summary-land-building", "AppraisalSummary", "Appraisal Summary – Land and Building", "ใบสรุปรายงานการประเมิน – ที่ดินและสิ่งปลูกสร้าง", "Async", true, "appraisal-summary-land-building", 1 },
                    { "appraisal-summary-machine", "AppraisalSummary", "Appraisal Summary – Machinery", "ใบสรุปรายงานการประเมิน – เครื่องจักร", "Async", true, "appraisal-summary-machine", 1 },
                    { "external-appraisal-report", "ExternalReport", "External Appraisal Report", "รายงานการประเมินราคา (บริษัทภายนอก)", "Async", true, "external-appraisal-report", 1 },
                    { "internal-report-block", "InternalReport", "Internal Block Project Report", "รายงานโครงการ (ภายใน)", "Async", true, "internal-report-block", 1 },
                    { "internal-report-construction", "InternalReport", "Internal Construction Inspection Report", "รายงานตรวจงานก่อสร้าง (ภายใน)", "Async", true, "internal-report-construction", 1 },
                    { "meeting-invitation", "Meeting", "Meeting Invitation", "หนังสือเชิญประชุม", "Sync", true, "meeting-invitation", 1 },
                    { "meeting-minute", "Meeting", "Meeting Minute", "รายงานการประชุม", "Sync", true, "meeting-minute", 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportDefinitions",
                schema: "reporting");
        }
    }
}
