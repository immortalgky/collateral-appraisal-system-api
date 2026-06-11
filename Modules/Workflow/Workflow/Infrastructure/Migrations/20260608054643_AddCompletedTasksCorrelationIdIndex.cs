using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedTasksCorrelationIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CompletedTasks_CorrelationId",
                schema: "workflow",
                table: "CompletedTasks",
                column: "CorrelationId")
                .Annotation("SqlServer:Include", new[] { "AssignedType", "AssignedTo", "AssigneeCompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompletedTasks_CorrelationId",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
