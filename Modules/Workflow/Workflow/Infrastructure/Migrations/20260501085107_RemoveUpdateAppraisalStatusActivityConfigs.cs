using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUpdateAppraisalStatusActivityConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove seed rows for the retired UpdateAppraisalStatus pipeline step.
            // Status transitions for in-flight appraisals are now driven by
            // WorkflowTransitionedIntegrationEvent consumed in the Appraisal module.
            migrationBuilder.Sql(
                "DELETE FROM workflow.ActivityProcessConfigurations WHERE ProcessorName = 'UpdateAppraisalStatus';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way migration: restoring the removed rows would require knowing the original
            // Ids and SortOrders. On a fresh database the seeder will re-create them, but since
            // the seeder now omits these rows the Down() is intentionally a no-op.
        }
    }
}
