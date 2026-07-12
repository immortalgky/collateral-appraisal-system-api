using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingTaskDecisionDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionTaken",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftAssignee",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "DecisionTaken",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "DraftAssignee",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.AlterColumn<string>(
                name: "Remark",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
