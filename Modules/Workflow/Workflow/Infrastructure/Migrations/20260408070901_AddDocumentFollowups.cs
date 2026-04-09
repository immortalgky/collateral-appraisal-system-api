using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentFollowups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentFollowups",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineItems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RaisingWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaisingPendingTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaisingActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RaisingUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FollowupWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFollowups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFollowups_FollowupWorkflowInstanceId",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "FollowupWorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFollowups_RaisingPendingTaskId_Status",
                schema: "workflow",
                table: "DocumentFollowups",
                columns: new[] { "RaisingPendingTaskId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFollowups_RaisingWorkflowInstanceId",
                schema: "workflow",
                table: "DocumentFollowups",
                column: "RaisingWorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFollowups_RequestId_Status",
                schema: "workflow",
                table: "DocumentFollowups",
                columns: new[] { "RequestId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentFollowups",
                schema: "workflow");
        }
    }
}
