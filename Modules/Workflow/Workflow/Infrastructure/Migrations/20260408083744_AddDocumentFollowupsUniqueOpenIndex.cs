using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentFollowupsUniqueOpenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "RaisingPendingTaskId",
                unique: true,
                filter: "[Status] = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_DocumentFollowups_RaisingPendingTaskId_Open",
                schema: "workflow",
                table: "DocumentFollowups");
        }
    }
}
