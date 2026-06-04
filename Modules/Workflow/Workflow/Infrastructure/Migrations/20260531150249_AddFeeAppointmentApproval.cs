using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeAppointmentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentApprovalRules",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeekendHolidayEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LeadTimeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    RescheduleEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RescheduleThreshold = table.Column<int>(type: "int", nullable: true),
                    AppliesTo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Ext"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentApprovalRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeAppointmentApprovals",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Lines = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestSource = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedTier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApproverAssignee = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    FollowupWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FeeAppointmentApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeApprovalTiers",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApproverCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignedType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TierLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AppliesTo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Ext"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeApprovalTiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeAppointmentApprovals_AppraisalId",
                schema: "workflow",
                table: "FeeAppointmentApprovals",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAppointmentApprovals_AppraisalId_Status",
                schema: "workflow",
                table: "FeeAppointmentApprovals",
                columns: new[] { "AppraisalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeAppointmentApprovals_FollowupWorkflowInstanceId",
                schema: "workflow",
                table: "FeeAppointmentApprovals",
                column: "FollowupWorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentApprovalRules",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "FeeAppointmentApprovals",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "FeeApprovalTiers",
                schema: "workflow");
        }
    }
}
