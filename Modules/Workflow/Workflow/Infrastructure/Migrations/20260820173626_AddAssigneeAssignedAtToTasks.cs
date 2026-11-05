using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <summary>
    /// Splits "when this holder received the task" out of AssignedAt.
    ///
    /// A supervisor reassign deliberately freezes AssignedAt (and DueAt/Sla*) so the SLA clock keeps
    /// running, while snapshotting an audit row into CompletedTasks. That left the outgoing and
    /// incoming rows sharing an identical AssignedAt, so the history timelines — which ordered on it
    /// alone — returned them in arbitrary order. AssigneeAssignedAt is re-stamped on hand-off and is
    /// what those timelines now order and display on; AssignedAt keeps its SLA meaning untouched.
    ///
    /// The backfill from AssignedAt was removed from Up(); only safe on an empty table.
    /// </summary>
    public partial class AddAssigneeAssignedAtToTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Land nullable first — a NOT NULL add would stamp every existing row with the CLR
            // default (0001-01-01) and sort the entire history backwards. It is made NOT NULL below;
            // the backfill in between was removed (only safe on an empty table).
            migrationBuilder.AddColumn<DateTime>(
                name: "AssigneeAssignedAt",
                schema: "workflow",
                table: "PendingTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssigneeAssignedAt",
                schema: "workflow",
                table: "CompletedTasks",
                type: "datetime2",
                nullable: true);

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.AlterColumn<DateTime>(
                name: "AssigneeAssignedAt",
                schema: "workflow",
                table: "PendingTasks",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AssigneeAssignedAt",
                schema: "workflow",
                table: "CompletedTasks",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssigneeAssignedAt",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "AssigneeAssignedAt",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
