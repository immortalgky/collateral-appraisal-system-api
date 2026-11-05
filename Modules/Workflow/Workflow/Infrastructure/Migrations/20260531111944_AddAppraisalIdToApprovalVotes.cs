using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalIdToApprovalVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add AppraisalId as nullable — no default, so existing rows get NULL.
            migrationBuilder.AddColumn<Guid>(
                name: "AppraisalId",
                schema: "workflow",
                table: "ApprovalVotes",
                type: "uniqueidentifier",
                nullable: true);

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            // Step 2: Make the column NOT NULL. The backfill between steps 1 and 2 was removed; only safe on an empty table.
            migrationBuilder.AlterColumn<Guid>(
                name: "AppraisalId",
                schema: "workflow",
                table: "ApprovalVotes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // Step 3: Create covering index for the appraisal-keyed history query.
            migrationBuilder.CreateIndex(
                name: "IX_ApprovalVotes_AppraisalId_ActivityId",
                schema: "workflow",
                table: "ApprovalVotes",
                columns: new[] { "AppraisalId", "ActivityId" })
                .Annotation("SqlServer:Include", new[] { "ActivityExecutionId", "VotedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalVotes_AppraisalId_ActivityId",
                schema: "workflow",
                table: "ApprovalVotes");

            migrationBuilder.DropColumn(
                name: "AppraisalId",
                schema: "workflow",
                table: "ApprovalVotes");
        }
    }
}
