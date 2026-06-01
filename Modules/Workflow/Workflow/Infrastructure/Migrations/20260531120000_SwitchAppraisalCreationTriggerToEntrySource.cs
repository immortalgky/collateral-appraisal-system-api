using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations;

/// <summary>
/// Repoints the appraisal-creation trigger conditions from the business <c>channel</c> to the
/// explicit <c>entrySource</c> discriminator (UI vs API). A UI-created request may carry a
/// non-MANUAL channel yet must still pass through appraisal-initiation-check, so the UI-vs-API
/// split must key on how the request entered the system, not on channel.
/// Idempotent UPDATEs; safe to re-run.
/// </summary>
public partial class SwitchAppraisalCreationTriggerToEntrySource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Non-UI entry (API + reappraisal): immediate appraisal creation at workflow start.
        migrationBuilder.Sql("""
            UPDATE workflow.ActivityProcessConfigurations
            SET ParametersJson = '{"condition": "entrySource != ''UI''"}',
                UpdatedAt = GETDATE(), UpdatedBy = 'system'
            WHERE ActivityName = '__on_workflow_start__'
              AND ProcessorName = 'EmitAppraisalCreationRequested'
            """);

        // UI entry: deferred appraisal creation at initiation-check approval.
        migrationBuilder.Sql("""
            UPDATE workflow.ActivityProcessConfigurations
            SET ParametersJson = '{"condition": "entrySource == ''UI''", "requireDecision": "P"}',
                UpdatedAt = GETDATE(), UpdatedBy = 'system'
            WHERE ActivityName = 'appraisal-initiation-check'
              AND ProcessorName = 'EmitAppraisalCreationRequested'
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE workflow.ActivityProcessConfigurations
            SET Parameters = '{"condition": "channel != ''MANUAL''"}',
                UpdatedAt = GETUTCDATE(), UpdatedBy = 'system'
            WHERE ActivityName = '__on_workflow_start__'
              AND ProcessorName = 'EmitAppraisalCreationRequested'
            """);

        migrationBuilder.Sql("""
            UPDATE workflow.ActivityProcessConfigurations
            SET Parameters = '{"condition": "channel == ''MANUAL''", "requireDecision": "P"}',
                UpdatedAt = GETUTCDATE(), UpdatedBy = 'system'
            WHERE ActivityName = 'appraisal-initiation-check'
              AND ProcessorName = 'EmitAppraisalCreationRequested'
            """);
    }
}
