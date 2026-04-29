using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingTaskCorrelationIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_CorrelationId_AssignedAt",
                schema: "workflow",
                table: "PendingTasks",
                columns: new[] { "CorrelationId", "AssignedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingTasks_CorrelationId_AssignedAt",
                schema: "workflow",
                table: "PendingTasks");
        }
    }
}
