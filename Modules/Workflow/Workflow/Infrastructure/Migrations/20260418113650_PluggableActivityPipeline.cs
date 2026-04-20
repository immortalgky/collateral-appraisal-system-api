using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PluggableActivityPipeline : Migration
    {
        // Action-style step names that should be classified as Kind = 1 (Action).
        // All other existing rows default to Kind = 0 (Validation).
        private static readonly string[] ActionStepNames =
        [
            "UpdateAppraisalStatus",
            "UpdateAssignmentStatus",
            "EmitAppraisalCreationRequested",
            "SetVariable"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ActivityProcessConfigurations: rename Parameters → ParametersJson ──
            migrationBuilder.RenameColumn(
                name: "Parameters",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                newName: "ParametersJson");

            // ── ActivityProcessConfigurations: new columns ──

            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0); // 0 = Validation

            migrationBuilder.AddColumn<string>(
                name: "RunIfExpression",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // ── Data migration: flip Kind = 1 for action-style steps ──
            foreach (var stepName in ActionStepNames)
            {
                migrationBuilder.Sql($"""
                    UPDATE workflow.ActivityProcessConfigurations
                    SET Kind = 1
                    WHERE ProcessorName = '{stepName}'
                    """);
            }

            // ── ActivityProcessExecutions: new trace table ──

            migrationBuilder.CreateTable(
                name: "ActivityProcessExecutions",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowActivityExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationVersion = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    RunIfExpressionSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParametersJsonSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<byte>(type: "tinyint", nullable: false),
                    SkipReason = table.Column<byte>(type: "tinyint", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityProcessExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityProcessExecutions_WorkflowActivityExecutionId",
                schema: "workflow",
                table: "ActivityProcessExecutions",
                column: "WorkflowActivityExecutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityProcessExecutions",
                schema: "workflow");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "workflow",
                table: "ActivityProcessConfigurations");

            migrationBuilder.DropColumn(
                name: "RunIfExpression",
                schema: "workflow",
                table: "ActivityProcessConfigurations");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "workflow",
                table: "ActivityProcessConfigurations");

            migrationBuilder.RenameColumn(
                name: "ParametersJson",
                schema: "workflow",
                table: "ActivityProcessConfigurations",
                newName: "Parameters");
        }
    }
}
