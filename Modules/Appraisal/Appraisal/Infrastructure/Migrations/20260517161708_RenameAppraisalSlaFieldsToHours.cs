using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppraisalSlaFieldsToHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SLADays",
                schema: "appraisal",
                table: "Appraisals",
                newName: "SLAHours");

            migrationBuilder.RenameColumn(
                name: "ActualDaysToComplete",
                schema: "appraisal",
                table: "Appraisals",
                newName: "ActualHoursToComplete");

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the data conversion before renaming back.
            migrationBuilder.Sql(
                "UPDATE [appraisal].[Appraisals] SET SLAHours = SLAHours / 8 WHERE SLAHours IS NOT NULL");
            migrationBuilder.Sql(
                "UPDATE [appraisal].[Appraisals] SET ActualHoursToComplete = ActualHoursToComplete / 8 WHERE ActualHoursToComplete IS NOT NULL");

            migrationBuilder.RenameColumn(
                name: "SLAHours",
                schema: "appraisal",
                table: "Appraisals",
                newName: "SLADays");

            migrationBuilder.RenameColumn(
                name: "ActualHoursToComplete",
                schema: "appraisal",
                table: "Appraisals",
                newName: "ActualDaysToComplete");
        }
    }
}
