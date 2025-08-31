using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Data.Migrations.Assignment
{
    /// <inheritdoc />
    public partial class AddWorkflowActivityExecutionCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityExecutions_WorkflowInstanceId",
                schema: "assignment",
                table: "WorkflowActivityExecutions");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_AssignedTo_Status",
                schema: "assignment",
                table: "WorkflowActivityExecutions",
                columns: new[] { "AssignedTo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_WorkflowInstanceId_Status",
                schema: "assignment",
                table: "WorkflowActivityExecutions",
                columns: new[] { "WorkflowInstanceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityExecutions_AssignedTo_Status",
                schema: "assignment",
                table: "WorkflowActivityExecutions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityExecutions_WorkflowInstanceId_Status",
                schema: "assignment",
                table: "WorkflowActivityExecutions");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_WorkflowInstanceId",
                schema: "assignment",
                table: "WorkflowActivityExecutions",
                column: "WorkflowInstanceId");
        }
    }
}
