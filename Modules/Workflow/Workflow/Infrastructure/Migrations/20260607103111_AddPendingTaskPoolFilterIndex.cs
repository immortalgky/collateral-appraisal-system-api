using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingTaskPoolFilterIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_AssignedType_AssignedTo_Company",
                schema: "workflow",
                table: "PendingTasks",
                columns: new[] { "AssignedType", "AssignedTo", "AssigneeCompanyId" })
                .Annotation("SqlServer:Include", new[] { "ActivityId", "WorkflowInstanceId", "CorrelationId", "AssignedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingTasks_AssignedType_AssignedTo_Company",
                schema: "workflow",
                table: "PendingTasks");
        }
    }
}
