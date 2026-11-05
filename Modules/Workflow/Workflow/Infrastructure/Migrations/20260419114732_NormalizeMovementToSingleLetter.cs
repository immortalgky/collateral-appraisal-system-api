using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeMovementToSingleLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.AlterColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "F",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "Forward");

            migrationBuilder.AlterColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "F",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "Forward");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Forward",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "F");

            migrationBuilder.AlterColumn<string>(
                name: "Movement",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Forward",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "F");

            // Restore single-letter codes back to long-form values
            migrationBuilder.Sql("UPDATE [workflow].[PendingTasks] SET [Movement] = 'Forward' WHERE [Movement] = 'F';");
            migrationBuilder.Sql("UPDATE [workflow].[PendingTasks] SET [Movement] = 'Backward' WHERE [Movement] = 'B';");
            migrationBuilder.Sql("UPDATE [workflow].[CompletedTasks] SET [Movement] = 'Forward' WHERE [Movement] = 'F';");
            migrationBuilder.Sql("UPDATE [workflow].[CompletedTasks] SET [Movement] = 'Backward' WHERE [Movement] = 'B';");
        }
    }
}
