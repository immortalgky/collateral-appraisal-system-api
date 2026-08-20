using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowAutoAssignmentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoAssignmentRules",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Channels = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EntrySources = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LoanTypes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priorities = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MinFacilityLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxFacilityLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ConditionExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoutingDecision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoAssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoAssignmentRules_Active_Priority",
                schema: "workflow",
                table: "AutoAssignmentRules",
                columns: new[] { "IsActive", "Priority" },
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoAssignmentRules",
                schema: "workflow");
        }
    }
}
