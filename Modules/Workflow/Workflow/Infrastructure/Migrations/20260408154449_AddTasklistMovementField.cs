using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTasklistMovementField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Forward");

            migrationBuilder.AddColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Forward");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Movement",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "Movement",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
