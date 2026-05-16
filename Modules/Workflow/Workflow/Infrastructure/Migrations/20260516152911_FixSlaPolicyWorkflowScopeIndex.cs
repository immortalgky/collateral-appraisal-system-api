using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <summary>
    /// CRITICAL FIX: The workflow-scope unique index on SlaPolicies was incorrectly created on
    /// (WorkflowDefinitionId) alone. CalculateWorkflowDueAtAsync filters by LoanType, so multiple
    /// rows per WorkflowDefinitionId keyed by LoanType are valid and intended — matching the original
    /// WorkflowSlaConfigurations unique index on (WorkflowDefinitionId, LoanType).
    ///
    /// DEPLOY NOTE: Any environment that already ran 20260516115603_AddSlaPolicyScopedUniqueIndexes
    /// before this fix may have had rows silently dropped by the NOT EXISTS clause on the single-column
    /// index. Re-seed Workflow-scope SlaPolicy rows after applying this migration.
    /// </summary>
    public partial class FixSlaPolicyWorkflowScopeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "WorkflowDefinitionId", "LoanType" },
                unique: true,
                filter: "[Scope] = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies",
                column: "WorkflowDefinitionId",
                unique: true,
                filter: "[Scope] = 3");
        }
    }
}
