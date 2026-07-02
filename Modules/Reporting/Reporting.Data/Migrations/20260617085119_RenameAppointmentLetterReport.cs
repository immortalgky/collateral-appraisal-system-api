using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppointmentLetterReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appointment-quotation-request");

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[] { "appointment-letter", "Appointment", "Appointment Letter", "หนังสือนัดหมาย", "Sync", true, "appointment-letter", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "reporting",
                table: "ReportDefinitions",
                keyColumn: "ReportTypeKey",
                keyValue: "appointment-letter");

            migrationBuilder.InsertData(
                schema: "reporting",
                table: "ReportDefinitions",
                columns: new[] { "ReportTypeKey", "Category", "DisplayNameEn", "DisplayNameTh", "GenerationMode", "IsEnabled", "TemplateId", "Version" },
                values: new object[] { "appointment-quotation-request", "Appointment", "Appointment Quotation Request", "แบบฟอร์มนัดหมายและใบเสนอราคา", "Sync", true, "appointment-quotation-request", 1 });
        }
    }
}
