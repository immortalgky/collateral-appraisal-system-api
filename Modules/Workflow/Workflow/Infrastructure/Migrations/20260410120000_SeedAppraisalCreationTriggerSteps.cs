using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations;

/// <summary>
/// Seeds ActivityProcessConfiguration rows for the configurable appraisal creation trigger.
/// Uses IF NOT EXISTS to be safe on both fresh and existing databases.
/// </summary>
public partial class SeedAppraisalCreationTriggerSteps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Non-manual channel: immediate appraisal creation at workflow start
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM workflow.ActivityProcessConfigurations
                WHERE ActivityName = '__on_workflow_start__'
                  AND ProcessorName = 'EmitAppraisalCreationRequested'
            )
            BEGIN
                INSERT INTO workflow.ActivityProcessConfigurations
                    (Id, ActivityName, StepName, ProcessorName, SortOrder, Parameters, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                VALUES
                    (NEWID(), '__on_workflow_start__', 'Emit appraisal creation (non-manual)', 'EmitAppraisalCreationRequested', 1,
                     '{"condition": "channel != ''MANUAL''"}', 1, GETUTCDATE(), 'system', GETUTCDATE(), 'system')
            END
            """);

        // Manual channel: deferred appraisal creation at initiation-check approval
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM workflow.ActivityProcessConfigurations
                WHERE ActivityName = 'appraisal-initiation-check'
                  AND ProcessorName = 'EmitAppraisalCreationRequested'
            )
            BEGIN
                INSERT INTO workflow.ActivityProcessConfigurations
                    (Id, ActivityName, StepName, ProcessorName, SortOrder, Parameters, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                VALUES
                    (NEWID(), 'appraisal-initiation-check', 'Emit appraisal creation (manual)', 'EmitAppraisalCreationRequested', 1,
                     '{"condition": "channel == ''MANUAL''", "requireDecision": "P"}', 1, GETUTCDATE(), 'system', GETUTCDATE(), 'system')
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM workflow.ActivityProcessConfigurations
            WHERE ProcessorName = 'EmitAppraisalCreationRequested'
              AND ActivityName IN ('__on_workflow_start__', 'appraisal-initiation-check')
            """);
    }
}
