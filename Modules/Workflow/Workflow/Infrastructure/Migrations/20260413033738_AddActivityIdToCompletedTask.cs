using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityIdToCompletedTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityId",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Backfill ActivityId for existing rows.
            // Strategy: join CompletedTasks → WorkflowInstances (via CorrelationId) →
            // WorkflowActivityExecutions (via WorkflowInstanceId + AssignedTo).
            // ROW_NUMBER handles the edge case where one person appears in multiple
            // activities of the same workflow instance — picks the most recent match.
            migrationBuilder.Sql("""
                WITH Matched AS (
                    SELECT
                        ct.Id                                        AS CompletedTaskId,
                        wae.ActivityId,
                        ROW_NUMBER() OVER (
                            PARTITION BY ct.Id
                            ORDER BY wae.StartedOn DESC
                        )                                            AS rn
                    FROM workflow.CompletedTasks ct
                    INNER JOIN workflow.WorkflowInstances wi
                        ON wi.CorrelationId = CAST(ct.CorrelationId AS nvarchar(36))
                    INNER JOIN workflow.WorkflowActivityExecutions wae
                        ON wae.WorkflowInstanceId = wi.Id
                       AND wae.AssignedTo = ct.AssignedTo
                       AND wae.Status = 'Completed'
                    WHERE ct.ActivityId IS NULL
                )
                UPDATE ct
                SET    ct.ActivityId = m.ActivityId
                FROM   workflow.CompletedTasks ct
                JOIN   Matched m ON m.CompletedTaskId = ct.Id AND m.rn = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityId",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
