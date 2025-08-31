using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Data.Migrations.Assignment
{
    /// <inheritdoc />
    public partial class TaskAssignmentConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskAssignmentConfigurations",
                schema: "assignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryStrategies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteBackStrategies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecificAssignee = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssigneeGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupervisorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReplacementUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdditionalConfiguration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignmentConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_ActivityId",
                schema: "assignment",
                table: "TaskAssignmentConfigurations",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_ActivityId_WorkflowDefinitionId",
                schema: "assignment",
                table: "TaskAssignmentConfigurations",
                columns: new[] { "ActivityId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_IsActive",
                schema: "assignment",
                table: "TaskAssignmentConfigurations",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskAssignmentConfigurations",
                schema: "assignment");
        }
    }
}
