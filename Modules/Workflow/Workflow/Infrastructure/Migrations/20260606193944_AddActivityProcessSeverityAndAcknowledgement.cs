using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityProcessSeverityAndAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Acknowledged",
                schema: "workflow",
                table: "ActivityProcessExecutions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                schema: "workflow",
                table: "ActivityProcessExecutions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedToken",
                schema: "workflow",
                table: "ActivityProcessExecutions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Severity",
                schema: "workflow",
                table: "ActivityProcessExecutions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Severity",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateIndex(
                name: "UX_ActivityProcessConfigurations_Activity_Processor",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                columns: new[] { "ActivityName", "ProcessorName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ActivityProcessConfigurations_Activity_Processor",
                schema: "workflow",
                table: "ActivityProcessConfigurations");

            migrationBuilder.DropColumn(
                name: "Acknowledged",
                schema: "workflow",
                table: "ActivityProcessExecutions");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                schema: "workflow",
                table: "ActivityProcessExecutions");

            migrationBuilder.DropColumn(
                name: "AcknowledgedToken",
                schema: "workflow",
                table: "ActivityProcessExecutions");

            migrationBuilder.DropColumn(
                name: "Severity",
                schema: "workflow",
                table: "ActivityProcessExecutions");

            migrationBuilder.DropColumn(
                name: "Severity",
                schema: "workflow",
                table: "ActivityProcessConfigurations");
        }
    }
}
