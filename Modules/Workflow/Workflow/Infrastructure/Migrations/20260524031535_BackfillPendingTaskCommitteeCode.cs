using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <summary>
    /// One-time backfill of <c>workflow.PendingTasks.CommitteeCode</c> for approvals that were
    /// already in-flight when the column was introduced. The resolved committee code lives in the
    /// workflow instance variables (<c>pending_approval_committeeCode</c>); reading that JSON once
    /// here is the one-time write that keeps the hot read path (views) off the JSON. Idempotent.
    /// </summary>
    public partial class BackfillPendingTaskCommitteeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE pt
                SET pt.CommitteeCode = JSON_VALUE(wi.Variables, '$.pending_approval_committeeCode')
                FROM workflow.PendingTasks pt
                JOIN workflow.WorkflowInstances wi ON wi.Id = pt.WorkflowInstanceId
                WHERE pt.ActivityId = 'pending-approval'
                  AND pt.CommitteeCode IS NULL
                  AND JSON_VALUE(wi.Variables, '$.pending_approval_committeeCode') IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-time data backfill; not reversed.
        }
    }
}
