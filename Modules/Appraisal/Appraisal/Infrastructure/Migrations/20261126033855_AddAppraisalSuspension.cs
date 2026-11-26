using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                schema: "appraisal",
                table: "Appraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppraisalSuspensionLogs",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalSuspensionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_Suspended",
                schema: "appraisal",
                table: "Appraisals",
                column: "IsSuspended",
                filter: "[IsSuspended] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSuspensionLogs_Appraisal_ActionAt",
                schema: "appraisal",
                table: "AppraisalSuspensionLogs",
                columns: new[] { "AppraisalId", "ActionAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalSuspensionLogs",
                schema: "appraisal");

            migrationBuilder.DropIndex(
                name: "IX_Appraisals_Suspended",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                schema: "appraisal",
                table: "Appraisals");
        }
    }
}
