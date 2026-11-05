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

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

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
