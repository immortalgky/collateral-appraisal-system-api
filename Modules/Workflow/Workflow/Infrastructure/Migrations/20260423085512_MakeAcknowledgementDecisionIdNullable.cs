using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeAcknowledgementDecisionIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppraisalAcknowledgementQueueItems_AppraisalDecisionId",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppraisalDecisionId",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "UX_AckQueueItems_AppraisalId_CommitteeId_Active",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                columns: new[] { "AppraisalId", "CommitteeId" },
                unique: true,
                filter: "[Status] IN ('PendingAcknowledgement', 'Included')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AckQueueItems_AppraisalId_CommitteeId_Active",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppraisalDecisionId",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAcknowledgementQueueItems_AppraisalDecisionId",
                schema: "workflow",
                table: "AppraisalAcknowledgementQueueItems",
                column: "AppraisalDecisionId",
                unique: true,
                filter: "[Status] IN ('PendingAcknowledgement', 'Included')");
        }
    }
}
