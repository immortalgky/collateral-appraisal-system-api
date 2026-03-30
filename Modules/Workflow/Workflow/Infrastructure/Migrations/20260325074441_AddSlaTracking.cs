using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WorkflowDueAt",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowSlaStatus",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                schema: "workflow",
                table: "PendingTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAt",
                schema: "workflow",
                table: "PendingTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                schema: "workflow",
                table: "PendingTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                schema: "workflow",
                table: "CompletedTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAt",
                schema: "workflow",
                table: "CompletedTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                schema: "workflow",
                table: "CompletedTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessHoursConfigs",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHoursConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaBreachLogs",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PendingTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BreachedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaBreachLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaConfigurations",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LoanType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DurationHours = table.Column<int>(type: "int", nullable: false),
                    UseBusinessDays = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSlaConfigurations",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalDurationHours = table.Column<int>(type: "int", nullable: false),
                    UseBusinessDays = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSlaConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                schema: "workflow",
                table: "Holidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Year",
                schema: "workflow",
                table: "Holidays",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_SlaBreachLogs_PendingTaskId_SlaStatus",
                schema: "workflow",
                table: "SlaBreachLogs",
                columns: new[] { "PendingTaskId", "SlaStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaConfigurations_ActivityId_WorkflowDefinitionId_CompanyId_LoanType",
                schema: "workflow",
                table: "SlaConfigurations",
                columns: new[] { "ActivityId", "WorkflowDefinitionId", "CompanyId", "LoanType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSlaConfigurations_WorkflowDefinitionId_LoanType",
                schema: "workflow",
                table: "WorkflowSlaConfigurations",
                columns: new[] { "WorkflowDefinitionId", "LoanType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessHoursConfigs",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "SlaBreachLogs",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "SlaConfigurations",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowSlaConfigurations",
                schema: "workflow");

            migrationBuilder.DropColumn(
                name: "WorkflowDueAt",
                schema: "workflow",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "WorkflowSlaStatus",
                schema: "workflow",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DueAt",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                schema: "workflow",
                table: "PendingTasks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                schema: "workflow",
                table: "CompletedTasks");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                schema: "workflow",
                table: "CompletedTasks");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
