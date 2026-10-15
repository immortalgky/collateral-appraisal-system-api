using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitteeIdToMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CommitteeId",
                schema: "workflow",
                table: "Meetings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingItems_WorkflowInstanceId",
                schema: "workflow",
                table: "MeetingItems",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingItems_WorkflowInstanceId",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "CommitteeId",
                schema: "workflow",
                table: "Meetings");
        }
    }
}
