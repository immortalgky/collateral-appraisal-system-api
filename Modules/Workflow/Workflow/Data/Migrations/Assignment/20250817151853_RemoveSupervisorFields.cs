using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Data.Migrations.Assignment
{
    /// <inheritdoc />
    public partial class RemoveSupervisorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacementUserId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.DropColumn(
                name: "SupervisorId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplacementUserId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
