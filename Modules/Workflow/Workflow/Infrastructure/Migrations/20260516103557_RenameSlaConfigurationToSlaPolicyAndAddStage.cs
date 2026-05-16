using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSlaConfigurationToSlaPolicyAndAddStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the table in-place to preserve all existing rows.
            migrationBuilder.Sql("EXEC sp_rename 'workflow.SlaConfigurations', 'SlaPolicies'");

            // Drop the old unique index that existed on SlaConfigurations before the rename.
            // The index name is still the old name at this point; SQL Server renames constraint names
            // automatically on table rename, but index names may not change. We drop and recreate.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SlaConfigurations_ActivityId_WorkflowDefinitionId_CompanyId_LoanType') " +
                "DROP INDEX [IX_SlaConfigurations_ActivityId_WorkflowDefinitionId_CompanyId_LoanType] ON [workflow].[SlaPolicies]");

            // Add Stage scope columns. All existing rows default to Scope = 1 (Activity).
            migrationBuilder.AddColumn<int>(
                name: "Scope",
                schema: "workflow",
                table: "SlaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "StartActivityKey",
                schema: "workflow",
                table: "SlaPolicies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndActivityKey",
                schema: "workflow",
                table: "SlaPolicies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleActivityKeys",
                schema: "workflow",
                table: "SlaPolicies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MiddleActivityKeys",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "EndActivityKey",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "StartActivityKey",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.Sql("EXEC sp_rename 'workflow.SlaPolicies', 'SlaConfigurations'");

            migrationBuilder.CreateIndex(
                name: "IX_SlaConfigurations_ActivityId_WorkflowDefinitionId_CompanyId_LoanType",
                schema: "workflow",
                table: "SlaConfigurations",
                columns: new[] { "ActivityId", "WorkflowDefinitionId", "CompanyId", "LoanType" },
                unique: true);
        }
    }
}
