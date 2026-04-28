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

            // 2. Remap Meetings.Status string values to match new MeetingStatus enum.
            //    Storage is string (HasConversion<string>()), so we update the literals.
            //    Draft → New, Scheduled → InvitationSent.
            //    Ended, Cancelled, and the new RoutedBack require no row changes.
            migrationBuilder.Sql("""
                UPDATE workflow.Meetings
                SET Status = CASE Status
                    WHEN 'Draft'      THEN 'New'
                    WHEN 'Scheduled'  THEN 'InvitationSent'
                    ELSE Status
                END
                WHERE Status IN ('Draft', 'Scheduled')
                """);
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
