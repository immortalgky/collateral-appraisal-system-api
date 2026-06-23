using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionInPendingTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignNextToType",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentDecision",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionType",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignNextToType",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "CommentDecision",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "DecisionType",
                schema: "workflow",
                table: "PendingTasks");
        }
    }
}
