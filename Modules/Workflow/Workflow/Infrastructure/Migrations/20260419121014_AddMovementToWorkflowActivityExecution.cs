using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementToWorkflowActivityExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "F");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Movement",
                schema: "workflow",
                table: "WorkflowActivityExecutions");
        }
    }
}
