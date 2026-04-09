using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastCompletedByToWorkflowInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastCompletedBy",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCompletedBy",
                schema: "workflow",
                table: "WorkflowInstances");
        }
    }
}
