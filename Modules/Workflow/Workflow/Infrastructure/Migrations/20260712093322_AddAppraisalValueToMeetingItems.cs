using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalValueToMeetingItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "workflow",
                table: "MeetingQueueItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "workflow",
                table: "MeetingItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "workflow",
                table: "MeetingQueueItems");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "workflow",
                table: "MeetingItems");
        }
    }
}
