using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEnumConverters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems");

            migrationBuilder.DropIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowOutboxes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems",
                column: "AppraisalId",
                unique: true,
                filter: "[Status] = 'ASSIGNED'");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "RaisingPendingTaskId",
                unique: true,
                filter: "[Status] = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems");

            migrationBuilder.DropIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowOutboxes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems",
                column: "AppraisalId",
                unique: true,
                filter: "[Status] = 'Assigned'");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "RaisingPendingTaskId",
                unique: true,
                filter: "[Status] = 'Open'");
        }
    }
}
