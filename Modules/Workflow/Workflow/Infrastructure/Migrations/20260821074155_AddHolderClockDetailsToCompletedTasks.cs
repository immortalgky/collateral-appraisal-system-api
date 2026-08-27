using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <summary>
    /// Carries OpenedAt / SlaStartAt / SlaDurationHours from PendingTask onto the archived row so the
    /// Decision and Summary history tooltips can show, per holder, when they actually opened the task
    /// and what the SLA clock was really anchored on (appointment-anchored and window-governed tasks
    /// do NOT anchor on AssignedAt).
    ///
    /// Deliberately NOT backfilled. Unlike AssigneeAssignedAt — where "= AssignedAt" was correct by
    /// construction for every pre-existing row — these three have no truth on historic rows: a
    /// completed task's open time is unrecoverable, and defaulting SlaStartAt to AssignedAt would be
    /// wrong for every appointment-anchored leg. NULL reads as "ไม่มีข้อมูล" and the tooltip omits
    /// the line, which beats fabricating a timestamp nobody can correct later.
    /// </summary>
    public partial class AddHolderClockDetailsToCompletedTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                schema: "workflow",
                table: "CompletedTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaDurationHours",
                schema: "workflow",
                table: "CompletedTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaStartAt",
                schema: "workflow",
                table: "CompletedTasks",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpenedAt",
                schema: "workflow",
                table: "CompletedTasks");

            migrationBuilder.DropColumn(
                name: "SlaDurationHours",
                schema: "workflow",
                table: "CompletedTasks");

            migrationBuilder.DropColumn(
                name: "SlaStartAt",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
