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
            // UserNotifications.Type: SCREAMING_SNAKE → PascalCase
            migrationBuilder.Sql("""
                UPDATE notification.UserNotifications
                SET Type = CASE Type
                    WHEN 'TASK_ASSIGNED'                THEN 'TaskAssigned'
                    WHEN 'TASK_COMPLETED'               THEN 'TaskCompleted'
                    WHEN 'WORKFLOW_TRANSITION'          THEN 'WorkflowTransition'
                    WHEN 'SYSTEM_NOTIFICATION'          THEN 'SystemNotification'
                    WHEN 'DOCUMENT_FOLLOWUP_RAISED'     THEN 'DocumentFollowupRaised'
                    WHEN 'DOCUMENT_FOLLOWUP_RESOLVED'   THEN 'DocumentFollowupResolved'
                    WHEN 'DOCUMENT_FOLLOWUP_CANCELLED'  THEN 'DocumentFollowupCancelled'
                    WHEN 'DOCUMENT_LINE_ITEM_DECLINED'  THEN 'DocumentLineItemDeclined'
                    ELSE Type
                END
                WHERE Type IN (
                    'TASK_ASSIGNED','TASK_COMPLETED','WORKFLOW_TRANSITION','SYSTEM_NOTIFICATION',
                    'DOCUMENT_FOLLOWUP_RAISED','DOCUMENT_FOLLOWUP_RESOLVED',
                    'DOCUMENT_FOLLOWUP_CANCELLED','DOCUMENT_LINE_ITEM_DECLINED'
                )
                """);
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
