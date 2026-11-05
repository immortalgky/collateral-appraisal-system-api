using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingLifecycleRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add CommitteeMembers.Attendance column with default 'Always'.
            //    EF sets DEFAULT 'Always' so existing rows are automatically backfilled.
            migrationBuilder.AddColumn<string>(
                name: "Attendance",
                schema: "workflow",
                table: "CommitteeMembers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Always");

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the Status remap.
            // RoutedBack is a new value unknown to the previous app version; map it to Scheduled
            // (the closest prior semantic — an open meeting with decisions still in progress).
            // All three remaps run in a single UPDATE so ordering is irrelevant.
            migrationBuilder.Sql("""
                UPDATE workflow.Meetings
                SET Status = CASE Status
                    WHEN 'New'             THEN 'Draft'
                    WHEN 'InvitationSent'  THEN 'Scheduled'
                    WHEN 'RoutedBack'      THEN 'Scheduled'
                    ELSE Status
                END
                WHERE Status IN ('New', 'InvitationSent', 'RoutedBack')
                """);

            migrationBuilder.DropColumn(
                name: "Attendance",
                schema: "workflow",
                table: "CommitteeMembers");
        }
    }
}
