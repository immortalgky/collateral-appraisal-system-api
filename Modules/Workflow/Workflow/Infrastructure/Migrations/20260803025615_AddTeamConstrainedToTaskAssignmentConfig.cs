using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamConstrainedToTaskAssignmentConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExcludeAssigneesFrom",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TeamConstrained",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludeAssigneesFrom",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.DropColumn(
                name: "TeamConstrained",
                schema: "workflow",
                table: "TaskAssignmentConfigurations");
        }
    }
}
