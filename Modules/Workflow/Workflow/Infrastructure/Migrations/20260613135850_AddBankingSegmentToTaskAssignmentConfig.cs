using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankingSegmentToTaskAssignmentConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentConfigurations_ActivityId_WorkflowDefinitionId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentConfigurations_IsActive",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.AddColumn<string>(
                name: "BankingSegment",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_TaskAssignmentConfigurations_Activity_Workflow_Segment_Active",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                columns: new[] { "ActivityId", "WorkflowDefinitionId", "BankingSegment" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_TaskAssignmentConfigurations_Activity_Workflow_Segment_Active",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.DropColumn(
                name: "BankingSegment",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_ActivityId_WorkflowDefinitionId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                columns: new[] { "ActivityId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_IsActive",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                column: "IsActive");
        }
    }
}
