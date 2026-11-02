using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations;

/// <summary>
/// Repoints the appraisal-creation trigger conditions from the business <c>channel</c> to the
/// explicit <c>entrySource</c> discriminator (UI vs API). A UI-created request may carry a
/// non-MANUAL channel yet must still pass through appraisal-initiation-check, so the UI-vs-API
/// split must key on how the request entered the system, not on channel.
/// The UPDATEs were removed, so Up is now a no-op.
/// </summary>
public partial class SwitchAppraisalCreationTriggerToEntrySource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
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
