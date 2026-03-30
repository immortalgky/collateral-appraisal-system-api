using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RenameDashboardUserIdToUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamWorkloadSummaries",
                schema: "common",
                table: "TeamWorkloadSummaries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyTaskSummaries",
                schema: "common",
                table: "DailyTaskSummaries");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "common",
                table: "TeamWorkloadSummaries");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "common",
                table: "DailyTaskSummaries");

            migrationBuilder.RenameColumn(
                name: "UserName",
                schema: "common",
                table: "TeamWorkloadSummaries",
                newName: "Username");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "common",
                table: "DailyTaskSummaries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamWorkloadSummaries",
                schema: "common",
                table: "TeamWorkloadSummaries",
                column: "Username");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyTaskSummaries",
                schema: "common",
                table: "DailyTaskSummaries",
                columns: new[] { "Date", "Username" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamWorkloadSummaries",
                schema: "common",
                table: "TeamWorkloadSummaries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyTaskSummaries",
                schema: "common",
                table: "DailyTaskSummaries");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "common",
                table: "DailyTaskSummaries");

            migrationBuilder.RenameColumn(
                name: "Username",
                schema: "common",
                table: "TeamWorkloadSummaries",
                newName: "UserName");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "common",
                table: "TeamWorkloadSummaries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "common",
                table: "DailyTaskSummaries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamWorkloadSummaries",
                schema: "common",
                table: "TeamWorkloadSummaries",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyTaskSummaries",
                schema: "common",
                table: "DailyTaskSummaries",
                columns: new[] { "Date", "UserId" });
        }
    }
}
