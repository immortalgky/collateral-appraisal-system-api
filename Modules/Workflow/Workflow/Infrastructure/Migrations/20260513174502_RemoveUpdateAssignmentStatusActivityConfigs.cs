using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUpdateAssignmentStatusActivityConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove seed rows for the retired UpdateAssignmentStatus pipeline step.
            // Assignment-status transitions are now driven by:
            //   1. Synchronous command handlers (Pending → Assigned).
            //   2. WorkflowTransitionedIntegrationEventHandler (mid-states).
            //   3. AppraisalApprovedIntegrationEventHandler / MarkApprovedByCommittee (terminal Completed).
            // Leaving rows of this kind in place would silently throw at runtime now that
            // AppraisalAssignment.Complete() requires the Verified pre-state.
            migrationBuilder.Sql(
                "DELETE FROM workflow.ActivityProcessConfigurations WHERE ProcessorName = 'UpdateAssignmentStatus';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way migration: the seeder no longer emits these rows, so a rollback would not
            // restore them anyway. Intentionally empty.
        }
    }
}
