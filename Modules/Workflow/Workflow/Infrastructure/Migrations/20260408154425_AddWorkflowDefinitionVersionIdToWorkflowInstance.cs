using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitionVersionIdToWorkflowInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: add the column as nullable so existing rows survive.
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "uniqueidentifier",
                nullable: true);

            // Step 2: backfill. For each existing instance, look up the WorkflowDefinitionVersion row where
            //   DefinitionId = instance.WorkflowDefinitionId AND Status = Published (1)
            // Fall back to the highest Version for that definition if no Published version exists.
            migrationBuilder.Sql(@"
                WITH ChosenVersion AS (
                    SELECT wi.Id AS InstanceId,
                           (
                               SELECT TOP 1 wdv.Id
                               FROM [workflow].[WorkflowDefinitionVersions] wdv
                               WHERE wdv.DefinitionId = wi.WorkflowDefinitionId
                                 AND wdv.Status = 1 -- Published
                               ORDER BY wdv.[Version] DESC
                           ) AS PublishedVersionId,
                           (
                               SELECT TOP 1 wdv.Id
                               FROM [workflow].[WorkflowDefinitionVersions] wdv
                               WHERE wdv.DefinitionId = wi.WorkflowDefinitionId
                               ORDER BY wdv.[Version] DESC
                           ) AS AnyVersionId
                    FROM [workflow].[WorkflowInstances] wi
                )
                UPDATE wi
                SET wi.WorkflowDefinitionVersionId = COALESCE(cv.PublishedVersionId, cv.AnyVersionId)
                FROM [workflow].[WorkflowInstances] wi
                JOIN ChosenVersion cv ON cv.InstanceId = wi.Id
                WHERE wi.WorkflowDefinitionVersionId IS NULL;
            ");

            // Step 3: alter to NOT NULL (any row that still has NULL at this point indicates a definition
            // with no versions at all — those rows must be cleaned up manually before running this migration).
            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // Step 4: index + FK.
            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "WorkflowDefinitionVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_WorkflowDefinitionVersions_WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances",
                column: "WorkflowDefinitionVersionId",
                principalSchema: "workflow",
                principalTable: "WorkflowDefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_WorkflowDefinitionVersions_WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionVersionId",
                schema: "workflow",
                table: "WorkflowInstances");
        }
    }
}
