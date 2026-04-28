using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowInstanceRowVersionAndUniqueCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M2: RowVersion for optimistic concurrency on WorkflowInstance
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // M5: unique (CorrelationId, WorkflowDefinitionId) — database-level dedup guard for
            // concurrent QuotationStartedIntegrationEvent retries.
            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CorrelationId_WorkflowDefinitionId",
                schema: "workflow",
                table: "WorkflowInstances",
                columns: new[] { "CorrelationId", "WorkflowDefinitionId" },
                unique: true,
                filter: "[CorrelationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_CorrelationId_WorkflowDefinitionId",
                schema: "workflow",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "workflow",
                table: "WorkflowInstances");
        }
    }
}
