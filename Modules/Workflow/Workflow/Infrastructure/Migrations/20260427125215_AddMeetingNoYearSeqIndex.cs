using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingNoYearSeqIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingNoYear_MeetingNoSeq",
                schema: "workflow",
                table: "Meetings",
                columns: new[] { "MeetingNoYear", "MeetingNoSeq" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Meetings_MeetingNoYear_MeetingNoSeq",
                schema: "workflow",
                table: "Meetings");
        }
    }
}
