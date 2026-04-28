using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingTaskAssigneeCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeCompanyId",
                schema: "workflow",
                table: "PendingTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_WorkflowInstance_Activity_Company",
                schema: "workflow",
                table: "PendingTasks",
                columns: new[] { "WorkflowInstanceId", "ActivityId", "AssigneeCompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingTasks_WorkflowInstance_Activity_Company",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "AssigneeCompanyId",
                schema: "workflow",
                table: "PendingTasks");
        }
    }
}
