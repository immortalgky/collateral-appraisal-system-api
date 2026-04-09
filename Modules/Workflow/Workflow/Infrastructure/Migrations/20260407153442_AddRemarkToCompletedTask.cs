using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRemarkToCompletedTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Remark",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remark",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
