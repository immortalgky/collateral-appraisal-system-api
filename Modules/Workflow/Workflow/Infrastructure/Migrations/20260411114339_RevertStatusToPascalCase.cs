using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevertStatusToPascalCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WorkflowInstances.Status
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowInstances
                SET Status = CASE Status
                    WHEN 'RUNNING'   THEN 'Running'
                    WHEN 'COMPLETED' THEN 'Completed'
                    WHEN 'FAILED'    THEN 'Failed'
                    WHEN 'CANCELLED' THEN 'Cancelled'
                    WHEN 'SUSPENDED' THEN 'Suspended'
                    ELSE Status
                END
                WHERE Status IN ('RUNNING','COMPLETED','FAILED','CANCELLED','SUSPENDED')
                """);

            // WorkflowInstances.WorkflowSlaStatus
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowInstances
                SET WorkflowSlaStatus = CASE WorkflowSlaStatus
                    WHEN 'ON_TIME'  THEN 'OnTime'
                    WHEN 'ON_TRACK' THEN 'OnTrack'
                    WHEN 'AT_RISK'  THEN 'AtRisk'
                    WHEN 'BREACHED' THEN 'Breached'
                    ELSE WorkflowSlaStatus
                END
                WHERE WorkflowSlaStatus IN ('ON_TIME','ON_TRACK','AT_RISK','BREACHED')
                """);

            // WorkflowActivityExecutions.Status
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowActivityExecutions
                SET Status = CASE Status
                    WHEN 'PENDING'     THEN 'Pending'
                    WHEN 'IN_PROGRESS' THEN 'InProgress'
                    WHEN 'COMPLETED'   THEN 'Completed'
                    WHEN 'FAILED'      THEN 'Failed'
                    WHEN 'SKIPPED'     THEN 'Skipped'
                    WHEN 'CANCELLED'   THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('PENDING','IN_PROGRESS','COMPLETED','FAILED','SKIPPED','CANCELLED')
                """);

            // WorkflowOutboxes.Status
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowOutboxes
                SET Status = CASE Status
                    WHEN 'PENDING'     THEN 'Pending'
                    WHEN 'PROCESSING'  THEN 'Processing'
                    WHEN 'PROCESSED'   THEN 'Processed'
                    WHEN 'FAILED'      THEN 'Failed'
                    WHEN 'DEAD_LETTER' THEN 'DeadLetter'
                    ELSE Status
                END
                WHERE Status IN ('PENDING','PROCESSING','PROCESSED','FAILED','DEAD_LETTER')
                """);

            // PendingTasks.TaskStatus
            migrationBuilder.Sql("""
                UPDATE workflow.PendingTasks
                SET TaskStatus = CASE TaskStatus
                    WHEN 'ASSIGNED'    THEN 'Assigned'
                    WHEN 'IN_PROGRESS' THEN 'InProgress'
                    WHEN 'INPROGRESS'  THEN 'InProgress'
                    WHEN 'COMPLETING'  THEN 'Completing'
                    WHEN 'COMPLETED'   THEN 'Completed'
                    ELSE TaskStatus
                END
                WHERE TaskStatus IN ('ASSIGNED','IN_PROGRESS','INPROGRESS','COMPLETING','COMPLETED')
                """);

            // CompletedTasks.TaskStatus
            migrationBuilder.Sql("""
                UPDATE workflow.CompletedTasks
                SET TaskStatus = CASE TaskStatus
                    WHEN 'ASSIGNED'    THEN 'Assigned'
                    WHEN 'IN_PROGRESS' THEN 'InProgress'
                    WHEN 'INPROGRESS'  THEN 'InProgress'
                    WHEN 'COMPLETING'  THEN 'Completing'
                    WHEN 'COMPLETED'   THEN 'Completed'
                    ELSE TaskStatus
                END
                WHERE TaskStatus IN ('ASSIGNED','IN_PROGRESS','INPROGRESS','COMPLETING','COMPLETED')
                """);

            // PendingTasks.SlaStatus
            migrationBuilder.Sql("""
                UPDATE workflow.PendingTasks
                SET SlaStatus = CASE SlaStatus
                    WHEN 'ON_TIME'  THEN 'OnTime'
                    WHEN 'ON_TRACK' THEN 'OnTrack'
                    WHEN 'AT_RISK'  THEN 'AtRisk'
                    WHEN 'BREACHED' THEN 'Breached'
                    ELSE SlaStatus
                END
                WHERE SlaStatus IN ('ON_TIME','ON_TRACK','AT_RISK','BREACHED')
                """);

            // Meetings.Status
            migrationBuilder.Sql("""
                UPDATE workflow.Meetings
                SET Status = CASE Status
                    WHEN 'DRAFT'      THEN 'Draft'
                    WHEN 'SCHEDULED'  THEN 'Scheduled'
                    WHEN 'ENDED'      THEN 'Ended'
                    WHEN 'CANCELLED'  THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('DRAFT','SCHEDULED','ENDED','CANCELLED')
                """);

            // MeetingQueueItems.Status (if table exists)
            migrationBuilder.Sql("""
                IF OBJECT_ID('workflow.MeetingQueueItems', 'U') IS NOT NULL
                BEGIN
                    UPDATE workflow.MeetingQueueItems
                    SET Status = CASE Status
                        WHEN 'QUEUED'   THEN 'Queued'
                        WHEN 'ASSIGNED' THEN 'Assigned'
                        WHEN 'RELEASED' THEN 'Released'
                        ELSE Status
                    END
                    WHERE Status IN ('QUEUED','ASSIGNED','RELEASED')
                END
                """);

            // DocumentFollowups.Status — must update data BEFORE altering the partial index
            migrationBuilder.Sql("""
                UPDATE workflow.DocumentFollowups
                SET Status = CASE Status
                    WHEN 'OPEN'      THEN 'Open'
                    WHEN 'RESOLVED'  THEN 'Resolved'
                    WHEN 'CANCELLED' THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('OPEN','RESOLVED','CANCELLED')
                """);

            migrationBuilder.DropIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems");

            migrationBuilder.DropIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowOutboxes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems",
                column: "AppraisalId",
                unique: true,
                filter: "[Status] = 'Assigned'");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "RaisingPendingTaskId",
                unique: true,
                filter: "[Status] = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DocumentFollowups.Status — update data BEFORE recreating the partial index
            migrationBuilder.Sql("""
                UPDATE workflow.DocumentFollowups
                SET Status = CASE Status
                    WHEN 'Open'      THEN 'OPEN'
                    WHEN 'Resolved'  THEN 'RESOLVED'
                    WHEN 'Cancelled' THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Open','Resolved','Cancelled')
                """);

            migrationBuilder.DropIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems");

            migrationBuilder.DropIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowOutboxes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems",
                column: "AppraisalId",
                unique: true,
                filter: "[Status] = 'ASSIGNED'");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "RaisingPendingTaskId",
                unique: true,
                filter: "[Status] = 'OPEN'");
        }
    }
}
