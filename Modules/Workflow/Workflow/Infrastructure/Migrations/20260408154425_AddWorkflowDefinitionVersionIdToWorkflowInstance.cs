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

            // Step 3: index + FK.
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
