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

            // Step 2: Backfill from WorkflowInstances.Variables JSON.
            migrationBuilder.Sql(@"
UPDATE av
SET av.AppraisalId = TRY_CAST(JSON_VALUE(wi.Variables, '$.appraisalId') AS UNIQUEIDENTIFIER)
FROM workflow.ApprovalVotes av
JOIN workflow.WorkflowInstances wi ON wi.Id = av.WorkflowInstanceId
WHERE av.AppraisalId IS NULL;
");

            // Step 3: Stamp un-backfillable rows with a sentinel. Old votes whose
            // WorkflowInstances.Variables lack '$.appraisalId' (pre-feature rows, or
            // route-back votes written before appraisalId was in variables) cannot be
            // matched. They belong to already-completed appraisals that could never show
            // history anyway, so this is no regression. Guid.Empty matches the write
            // path's '?? Guid.Empty' fallback and is never matched by the history
            // endpoint (always queried with a real appraisalId).
            migrationBuilder.Sql(@"
UPDATE workflow.ApprovalVotes
SET AppraisalId = '00000000-0000-0000-0000-000000000000'
WHERE AppraisalId IS NULL;
");

            // Step 4: Make the column NOT NULL now that every row has a value.
            migrationBuilder.AlterColumn<Guid>(
                name: "AppraisalId",
                schema: "workflow",
                table: "ApprovalVotes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // Step 5: Create covering index for the appraisal-keyed history query.
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
