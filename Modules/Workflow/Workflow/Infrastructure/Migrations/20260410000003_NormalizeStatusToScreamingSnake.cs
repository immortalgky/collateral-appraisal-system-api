using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeStatusToScreamingSnake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WorkflowInstances.Status (was stored as PascalCase via HasConversion<string>())
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowInstances
                SET Status = CASE Status
                    WHEN 'Running'   THEN 'RUNNING'
                    WHEN 'Completed' THEN 'COMPLETED'
                    WHEN 'Failed'    THEN 'FAILED'
                    WHEN 'Cancelled' THEN 'CANCELLED'
                    WHEN 'Suspended' THEN 'SUSPENDED'
                    ELSE Status
                END
                WHERE Status IN ('Running','Completed','Failed','Cancelled','Suspended')
                """);

            // WorkflowInstances.WorkflowSlaStatus
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowInstances
                SET WorkflowSlaStatus = CASE WorkflowSlaStatus
                    WHEN 'OnTime'   THEN 'ON_TIME'
                    WHEN 'AtRisk'   THEN 'AT_RISK'
                    WHEN 'Breached' THEN 'BREACHED'
                    ELSE WorkflowSlaStatus
                END
                WHERE WorkflowSlaStatus IN ('OnTime','AtRisk','Breached')
                """);

            // WorkflowActivityExecutions.Status
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowActivityExecutions
                SET Status = CASE Status
                    WHEN 'Pending'    THEN 'PENDING'
                    WHEN 'InProgress' THEN 'IN_PROGRESS'
                    WHEN 'Completed'  THEN 'COMPLETED'
                    WHEN 'Failed'     THEN 'FAILED'
                    WHEN 'Skipped'    THEN 'SKIPPED'
                    WHEN 'Cancelled'  THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Pending','InProgress','Completed','Failed','Skipped','Cancelled')
                """);

            // WorkflowOutboxes.Status
            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowOutboxes
                SET Status = CASE Status
                    WHEN 'Pending'    THEN 'PENDING'
                    WHEN 'Processing' THEN 'PROCESSING'
                    WHEN 'Processed'  THEN 'PROCESSED'
                    WHEN 'Failed'     THEN 'FAILED'
                    WHEN 'DeadLetter' THEN 'DEAD_LETTER'
                    ELSE Status
                END
                WHERE Status IN ('Pending','Processing','Processed','Failed','DeadLetter')
                """);

            // PendingTasks.TaskStatus (INPROGRESS had no word separator — fix it)
            migrationBuilder.Sql("""
                UPDATE workflow.PendingTasks
                SET TaskStatus = 'IN_PROGRESS'
                WHERE TaskStatus = 'INPROGRESS'
                """);

            // CompletedTasks.TaskStatus
            migrationBuilder.Sql("""
                UPDATE workflow.CompletedTasks
                SET TaskStatus = 'IN_PROGRESS'
                WHERE TaskStatus = 'INPROGRESS'
                """);

            // PendingTasks.SlaStatus
            migrationBuilder.Sql("""
                UPDATE workflow.PendingTasks
                SET SlaStatus = CASE SlaStatus
                    WHEN 'OnTime'   THEN 'ON_TIME'
                    WHEN 'AtRisk'   THEN 'AT_RISK'
                    WHEN 'Breached' THEN 'BREACHED'
                    ELSE SlaStatus
                END
                WHERE SlaStatus IN ('OnTime','AtRisk','Breached')
                """);

            // Meetings.Status
            migrationBuilder.Sql("""
                UPDATE workflow.Meetings
                SET Status = CASE Status
                    WHEN 'Draft'     THEN 'DRAFT'
                    WHEN 'Scheduled' THEN 'SCHEDULED'
                    WHEN 'Ended'     THEN 'ENDED'
                    WHEN 'Cancelled' THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Draft','Scheduled','Ended','Cancelled')
                """);

            // MeetingQueueItems.Status
            migrationBuilder.Sql("""
                IF OBJECT_ID('workflow.MeetingQueueItems', 'U') IS NOT NULL
                BEGIN
                    UPDATE workflow.MeetingQueueItems
                    SET Status = CASE Status
                        WHEN 'Queued'   THEN 'QUEUED'
                        WHEN 'Assigned' THEN 'ASSIGNED'
                        WHEN 'Released' THEN 'RELEASED'
                        ELSE Status
                    END
                    WHERE Status IN ('Queued','Assigned','Released')
                END
                """);

            // DocumentFollowups.Status
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

            // Update the unique partial index filter (requires drop + recreate)
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DocumentFollowups_RaisingPendingTaskId_Open')
                    DROP INDEX [UX_DocumentFollowups_RaisingPendingTaskId_Open] ON [workflow].[DocumentFollowups]
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX [UX_DocumentFollowups_RaisingPendingTaskId_Open]
                    ON [workflow].[DocumentFollowups] ([RaisingPendingTaskId])
                    WHERE [Status] = 'OPEN'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE workflow.PendingTasks
                SET TaskStatus = 'INPROGRESS'
                WHERE TaskStatus = 'IN_PROGRESS'
                """);

            migrationBuilder.Sql("""
                UPDATE workflow.CompletedTasks
                SET TaskStatus = 'INPROGRESS'
                WHERE TaskStatus = 'IN_PROGRESS'
                """);

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

            migrationBuilder.Sql("""
                UPDATE workflow.WorkflowInstances
                SET WorkflowSlaStatus = CASE WorkflowSlaStatus
                    WHEN 'ON_TIME'  THEN 'OnTime'
                    WHEN 'AT_RISK'  THEN 'AtRisk'
                    WHEN 'BREACHED' THEN 'Breached'
                    ELSE WorkflowSlaStatus
                END
                WHERE WorkflowSlaStatus IN ('ON_TIME','AT_RISK','BREACHED')
                """);

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

            migrationBuilder.Sql("""
                UPDATE workflow.PendingTasks
                SET SlaStatus = CASE SlaStatus
                    WHEN 'ON_TIME'  THEN 'OnTime'
                    WHEN 'AT_RISK'  THEN 'AtRisk'
                    WHEN 'BREACHED' THEN 'Breached'
                    ELSE SlaStatus
                END
                WHERE SlaStatus IN ('ON_TIME','AT_RISK','BREACHED')
                """);

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

            // Restore the unique partial index with old filter value
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DocumentFollowups_RaisingPendingTaskId_Open')
                    DROP INDEX [UX_DocumentFollowups_RaisingPendingTaskId_Open] ON [workflow].[DocumentFollowups]
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX [UX_DocumentFollowups_RaisingPendingTaskId_Open]
                    ON [workflow].[DocumentFollowups] ([RaisingPendingTaskId])
                    WHERE [Status] = 'Open'
                """);
        }
    }
}
