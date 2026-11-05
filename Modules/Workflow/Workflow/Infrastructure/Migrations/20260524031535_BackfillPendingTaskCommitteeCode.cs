using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <summary>
    /// One-time backfill (SQL since removed, so Up is a no-op) of <c>workflow.PendingTasks.CommitteeCode</c> for approvals that were
    /// already in-flight when the column was introduced. The resolved committee code lives in the
    /// workflow instance variables (<c>pending_approval_committeeCode</c>); reading that JSON once
    /// here was the one-time write that keeps the hot read path (views) off the JSON.
    /// </summary>
    public partial class BackfillPendingTaskCommitteeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-time data backfill; not reversed.
        }
    }
}
