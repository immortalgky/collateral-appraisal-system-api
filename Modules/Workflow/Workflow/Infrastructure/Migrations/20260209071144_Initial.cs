using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "CompletedTasks",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaskStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AssignedType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletedTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingTasks",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaskStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AssignedType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoundRobinQueue",
                schema: "workflow",
                columns: table => new
                {
                    ActivityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GroupsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GroupsList = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssignmentCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundRobinQueue", x => new { x.ActivityName, x.GroupsHash, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignmentConfigurations",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryStrategies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteBackStrategies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdminPoolId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EscalateToAdminPool = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SpecificAssignee = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssigneeGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdditionalConfiguration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignmentConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    JsonDefinition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionVersions",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    JsonSchema = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MigrationInstructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BreakingChanges = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeprecatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeprecatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CurrentActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentAssignee = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Variables = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuntimeOverrides = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "workflow",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowActivityExecutions",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActivityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InputData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowActivityExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowActivityExecutions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "workflow",
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowBookmarks",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsConsumed = table.Column<bool>(type: "bit", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcurrencyToken = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowBookmarks_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "workflow",
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowExecutionLogs",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Event = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowExecutionLogs_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "workflow",
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowExternalCalls",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true),
                    ConcurrencyToken = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowExternalCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowExternalCalls_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "workflow",
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowOutboxes",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConcurrencyToken = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowOutboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowOutboxes_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "workflow",
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAssignmentCounters_Selection",
                schema: "workflow",
                table: "RoundRobinQueue",
                columns: new[] { "ActivityName", "GroupsHash", "IsActive", "AssignmentCount" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_ActivityId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_ActivityId_WorkflowDefinitionId",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                columns: new[] { "ActivityId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentConfigurations_IsActive",
                schema: "workflow",
                table: "TaskAssignmentConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_ActivityId",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_AssignedTo",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_AssignedTo_Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                columns: new[] { "AssignedTo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_StartedOn",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                column: "StartedOn");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityExecutions_WorkflowInstanceId_Status",
                schema: "workflow",
                table: "WorkflowActivityExecutions",
                columns: new[] { "WorkflowInstanceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_ActivityId",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_ClaimedBy",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "ClaimedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_Correlation_Type_Consumed",
                schema: "workflow",
                table: "WorkflowBookmarks",
                columns: new[] { "CorrelationId", "Type", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_CorrelationId",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_CreatedAt",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_DueAt",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_Instance_Activity_Consumed",
                schema: "workflow",
                table: "WorkflowBookmarks",
                columns: new[] { "WorkflowInstanceId", "ActivityId", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_IsConsumed",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "IsConsumed");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_Key_Type_Consumed",
                schema: "workflow",
                table: "WorkflowBookmarks",
                columns: new[] { "Key", "Type", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_LeaseExpiresAt",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_Type",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_Type_Consumed_Claim",
                schema: "workflow",
                table: "WorkflowBookmarks",
                columns: new[] { "Type", "IsConsumed", "ClaimedBy", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_Type_Consumed_Due",
                schema: "workflow",
                table: "WorkflowBookmarks",
                columns: new[] { "Type", "IsConsumed", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowBookmarks_WorkflowInstanceId",
                schema: "workflow",
                table: "WorkflowBookmarks",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Category",
                schema: "workflow",
                table: "WorkflowDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_IsActive",
                schema: "workflow",
                table: "WorkflowDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Name_Version",
                schema: "workflow",
                table: "WorkflowDefinitions",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_Category",
                schema: "workflow",
                table: "WorkflowDefinitionVersions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_DefinitionId",
                schema: "workflow",
                table: "WorkflowDefinitionVersions",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_DefinitionId_Version",
                schema: "workflow",
                table: "WorkflowDefinitionVersions",
                columns: new[] { "DefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_PublishedAt",
                schema: "workflow",
                table: "WorkflowDefinitionVersions",
                column: "PublishedAt",
                filter: "PublishedAt IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_Status",
                schema: "workflow",
                table: "WorkflowDefinitionVersions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionVersions_Status_DefinitionId_Version",
                schema: "workflow",
                table: "WorkflowDefinitionVersions",
                columns: new[] { "Status", "DefinitionId", "Version" },
                filter: "Status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_Activity_Event_Occurred",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                columns: new[] { "ActivityId", "Event", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_ActivityId",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_ActorId",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_Analytics",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                columns: new[] { "OccurredAt", "Event", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_CorrelationId",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_Event",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                column: "Event");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_Event_Occurred",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                columns: new[] { "Event", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_Instance_Occurred",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                columns: new[] { "WorkflowInstanceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_OccurredAt",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutionLogs_WorkflowInstanceId",
                schema: "workflow",
                table: "WorkflowExecutionLogs",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_ActivityId",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_CreatedAt",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_IdempotencyKey",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_Instance_Activity_Status",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                columns: new[] { "WorkflowInstanceId", "ActivityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_Status",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_Status_Created",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExternalCalls_WorkflowInstanceId",
                schema: "workflow",
                table: "WorkflowExternalCalls",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CorrelationId",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentAssignee",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "CurrentAssignee");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_StartedOn",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "StartedOn");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowDefinitionId",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_Analytics",
                schema: "workflow",
                table: "WorkflowOutboxes",
                columns: new[] { "Type", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_CorrelationId",
                schema: "workflow",
                table: "WorkflowOutboxes",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_NextAttemptAt",
                schema: "workflow",
                table: "WorkflowOutboxes",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_OccurredAt",
                schema: "workflow",
                table: "WorkflowOutboxes",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_Processing",
                schema: "workflow",
                table: "WorkflowOutboxes",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_Retry",
                schema: "workflow",
                table: "WorkflowOutboxes",
                columns: new[] { "Status", "Attempts", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_Status",
                schema: "workflow",
                table: "WorkflowOutboxes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_Type",
                schema: "workflow",
                table: "WorkflowOutboxes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowOutboxes_WorkflowInstanceId",
                schema: "workflow",
                table: "WorkflowOutboxes",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompletedTasks",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "PendingTasks",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "RoundRobinQueue",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "TaskAssignmentConfigurations",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowActivityExecutions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowBookmarks",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionVersions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowExecutionLogs",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowExternalCalls",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowOutboxes",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowInstances",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions",
                schema: "workflow");
        }
    }
}
