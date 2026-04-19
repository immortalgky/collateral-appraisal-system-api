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
            // Normalize existing rows before changing the default constraint
            migrationBuilder.Sql("UPDATE [workflow].[PendingTasks] SET [Movement] = 'F' WHERE [Movement] = 'Forward';");
            migrationBuilder.Sql("UPDATE [workflow].[PendingTasks] SET [Movement] = 'B' WHERE [Movement] = 'Backward';");
            migrationBuilder.Sql("UPDATE [workflow].[CompletedTasks] SET [Movement] = 'F' WHERE [Movement] = 'Forward';");
            migrationBuilder.Sql("UPDATE [workflow].[CompletedTasks] SET [Movement] = 'B' WHERE [Movement] = 'Backward';");

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
