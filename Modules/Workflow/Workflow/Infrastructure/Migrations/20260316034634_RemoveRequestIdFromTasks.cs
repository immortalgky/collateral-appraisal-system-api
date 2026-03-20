using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRequestIdFromTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestId",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "RequestId",
                schema: "workflow",
                table: "CompletedTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RequestId",
                schema: "workflow",
                table: "PendingTasks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RequestId",
                schema: "workflow",
                table: "CompletedTasks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
