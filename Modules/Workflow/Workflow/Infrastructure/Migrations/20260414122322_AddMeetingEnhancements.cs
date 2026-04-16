using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingEnhancements : Migration
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

            migrationBuilder.RenameColumn(
                name: "ScheduledAt",
                schema: "workflow",
                table: "Meetings",
                newName: "StartAt");

            migrationBuilder.RenameColumn(
                name: "Role",
                schema: "workflow",
                table: "CommitteeMembers",
                newName: "Position");

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

            migrationBuilder.AddColumn<string>(
                name: "AgendaCertifyMinutes",
                schema: "workflow",
                table: "Meetings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgendaChairmanInformed",
                schema: "workflow",
                table: "Meetings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgendaOthers",
                schema: "workflow",
                table: "Meetings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CutOffAt",
                schema: "workflow",
                table: "Meetings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAt",
                schema: "workflow",
                table: "Meetings",
                type: "datetime2",
                nullable: true);

            // Backfill EndAt for existing meetings: default to StartAt + 2 hours
            // (preserves the old single-datetime behavior where ScheduledAt implied a short window).
            migrationBuilder.Sql(
                "UPDATE workflow.Meetings SET EndAt = DATEADD(HOUR, 2, StartAt) WHERE EndAt IS NULL AND StartAt IS NOT NULL");

            migrationBuilder.AddColumn<string>(
                name: "FromText",
                schema: "workflow",
                table: "Meetings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationSentAt",
                schema: "workflow",
                table: "Meetings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingNo",
                schema: "workflow",
                table: "Meetings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeetingNoSeq",
                schema: "workflow",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeetingNoYear",
                schema: "workflow",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "workflow",
                table: "Meetings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ToText",
                schema: "workflow",
                table: "Meetings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowInstanceId",
                schema: "workflow",
                table: "MeetingItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ActivityId",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementGroup",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppraisalType",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                schema: "workflow",
                table: "MeetingItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionBy",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemDecision",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Decision");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceAppraisalDecisionId",
                schema: "workflow",
                table: "MeetingItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppraisalAcknowledgementQueueItems",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppraisalDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnqueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalAcknowledgementQueueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMembers",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceCommitteeMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMembers_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "workflow",
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingNo",
                schema: "workflow",
                table: "Meetings",
                column: "MeetingNo",
                unique: true,
                filter: "[MeetingNo] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAcknowledgementQueueItems_AppraisalDecisionId",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                column: "AppraisalDecisionId",
                unique: true,
                filter: "[Status] IN ('PendingAcknowledgement', 'Included')");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAcknowledgementQueueItems_MeetingId",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAcknowledgementQueueItems_Status",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMembers_MeetingId",
                schema: "workflow",
                table: "MeetingMembers",
                column: "MeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalAcknowledgementQueueItems",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "MeetingMembers",
                schema: "workflow");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_MeetingNo",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_MeetingQueueItems_AppraisalId",
                schema: "workflow",
                table: "MeetingQueueItems");

            migrationBuilder.DropIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups");

            migrationBuilder.DropColumn(
                name: "AgendaCertifyMinutes",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "AgendaChairmanInformed",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "AgendaOthers",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "CutOffAt",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "EndAt",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "FromText",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "InvitationSentAt",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "MeetingNo",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "MeetingNoSeq",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "MeetingNoYear",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "ToText",
                schema: "workflow",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "AcknowledgementGroup",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "AppraisalType",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "DecisionBy",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "ItemDecision",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.DropColumn(
                name: "SourceAppraisalDecisionId",
                schema: "workflow",
                table: "MeetingItems");

            migrationBuilder.RenameColumn(
                name: "StartAt",
                schema: "workflow",
                table: "Meetings",
                newName: "ScheduledAt");

            migrationBuilder.RenameColumn(
                name: "Position",
                schema: "workflow",
                table: "CommitteeMembers",
                newName: "Role");

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

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowInstanceId",
                schema: "workflow",
                table: "MeetingItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActivityId",
                schema: "workflow",
                table: "MeetingItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

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
    }
}
