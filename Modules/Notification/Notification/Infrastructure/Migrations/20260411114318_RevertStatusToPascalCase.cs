using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Migrations
{
    /// <inheritdoc />
    public partial class RevertStatusToPascalCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE notification.UserNotifications
                SET Type = CASE Type
                    WHEN 'TaskAssigned'               THEN 'TASK_ASSIGNED'
                    WHEN 'TaskCompleted'              THEN 'TASK_COMPLETED'
                    WHEN 'WorkflowTransition'         THEN 'WORKFLOW_TRANSITION'
                    WHEN 'SystemNotification'         THEN 'SYSTEM_NOTIFICATION'
                    WHEN 'DocumentFollowupRaised'     THEN 'DOCUMENT_FOLLOWUP_RAISED'
                    WHEN 'DocumentFollowupResolved'   THEN 'DOCUMENT_FOLLOWUP_RESOLVED'
                    WHEN 'DocumentFollowupCancelled'  THEN 'DOCUMENT_FOLLOWUP_CANCELLED'
                    WHEN 'DocumentLineItemDeclined'   THEN 'DOCUMENT_LINE_ITEM_DECLINED'
                    ELSE Type
                END
                WHERE Type IN (
                    'TaskAssigned','TaskCompleted','WorkflowTransition','SystemNotification',
                    'DocumentFollowupRaised','DocumentFollowupResolved',
                    'DocumentFollowupCancelled','DocumentLineItemDeclined'
                )
                """);
        }
    }
}
