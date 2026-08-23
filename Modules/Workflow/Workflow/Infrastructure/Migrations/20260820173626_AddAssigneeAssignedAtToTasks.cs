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
    /// Backfilled from AssignedAt, which is exactly right for every pre-existing row: before this
    /// column existed the two values were by definition the same moment.
    /// </summary>
    public partial class AddAssigneeAssignedAtToTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Land nullable first — a NOT NULL add would stamp every existing row with the CLR
            // default (0001-01-01) and sort the entire history backwards.
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

            migrationBuilder.Sql(
                "UPDATE workflow.PendingTasks SET AssigneeAssignedAt = AssignedAt WHERE AssigneeAssignedAt IS NULL;");

            migrationBuilder.Sql(
                "UPDATE workflow.CompletedTasks SET AssigneeAssignedAt = AssignedAt WHERE AssigneeAssignedAt IS NULL;");

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
