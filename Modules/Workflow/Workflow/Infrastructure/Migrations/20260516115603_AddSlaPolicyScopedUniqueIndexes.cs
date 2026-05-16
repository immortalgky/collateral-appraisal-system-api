using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaPolicyScopedUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the primary-key constraint that still carries the old SlaConfigurations name.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_SlaConfigurations') " +
                "EXEC sp_rename 'workflow.PK_SlaConfigurations', 'PK_SlaPolicies', 'OBJECT'");

            // o3: Backfill existing WorkflowSlaConfiguration rows into SlaPolicies as Scope=3 rows
            // so that CalculateWorkflowDueAtAsync now reads from the single SlaPolicies table.
            // ActivityId is set to '*' (wildcard) as a non-null placeholder required by the column constraint.
            // NOTE: audit columns on SlaPolicies are CreatedAt/UpdatedAt (set by the interceptor).
            migrationBuilder.Sql(@"
                INSERT INTO [workflow].[SlaPolicies]
                    (Id, ActivityId, WorkflowDefinitionId, CompanyId, LoanType,
                     DurationHours, UseBusinessDays, Priority, Scope,
                     StartActivityKey, EndActivityKey, MiddleActivityKeys,
                     CreatedAt, UpdatedAt)
                SELECT
                    Id,
                    '*'           AS ActivityId,
                    WorkflowDefinitionId,
                    NULL          AS CompanyId,
                    LoanType,
                    TotalDurationHours AS DurationHours,
                    UseBusinessDays,
                    Priority,
                    3             AS Scope,      -- SlaPolicyScope.Workflow
                    NULL          AS StartActivityKey,
                    NULL          AS EndActivityKey,
                    NULL          AS MiddleActivityKeys,
                    CreatedAt,
                    UpdatedAt
                FROM [workflow].[WorkflowSlaConfigurations]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [workflow].[SlaPolicies] sp
                    WHERE sp.WorkflowDefinitionId = [workflow].[WorkflowSlaConfigurations].WorkflowDefinitionId
                      AND sp.Scope = 3
                      AND ((sp.LoanType IS NULL AND [workflow].[WorkflowSlaConfigurations].LoanType IS NULL) OR sp.LoanType = [workflow].[WorkflowSlaConfigurations].LoanType)
                )");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Activity",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "ActivityId", "WorkflowDefinitionId", "CompanyId", "LoanType", "Priority" },
                unique: true,
                filter: "[Scope] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Stage_Start",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "StartActivityKey", "WorkflowDefinitionId", "CompanyId", "LoanType", "Priority" },
                unique: true,
                filter: "[Scope] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies",
                column: "WorkflowDefinitionId",
                unique: true,
                filter: "[Scope] = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Activity",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Stage_Start",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies");
        }
    }
}
