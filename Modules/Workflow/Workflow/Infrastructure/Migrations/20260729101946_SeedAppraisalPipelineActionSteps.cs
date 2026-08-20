using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <summary>
    /// Seeds the three Action-kind pipeline steps that drive appraisal creation and the internal
    /// assignment-type flag.
    ///
    /// These rows were originally written as two migrations (SeedAppraisalCreationTriggerSteps,
    /// SeedSetAssignmentTypeStep) that shipped without a <c>.Designer.cs</c> and without an
    /// inline <c>[Migration]</c> attribute, so EF never discovered them — they were absent from
    /// <c>migrations list</c>, from <c>migrations script</c>, and therefore from every database.
    /// Nothing else creates them: neither ActivityProcessConfigurationSeeder nor any DbUp
    /// script. Without them <c>EmitAppraisalCreationRequested</c> never runs and no appraisal is
    /// created when a workflow starts.
    ///
    /// Re-dated to the end of the chain rather than restored in place. EF applies a missing
    /// migration against the CURRENT schema — it does not rewind — so the original 2026-04 ids
    /// would have run their <c>INSERT … (Parameters, …)</c> against a table where 20260418113650
    /// (PluggableActivityPipeline) renamed that column to ParametersJson, failing with
    /// "Invalid column name 'Parameters'" on every existing database. Re-dating is safe
    /// precisely because these were never applied anywhere.
    ///
    /// Running last also means 20260531120000 (SwitchAppraisalCreationTriggerToEntrySource) has
    /// already gone by, so the final entrySource-based conditions are seeded directly rather
    /// than the superseded channel-based ones.
    ///
    /// Idempotent: every insert is IF NOT EXISTS on (ActivityName, ProcessorName), matching the
    /// natural key ActivityProcessConfigurationSeeder reconciles on.
    /// </summary>
    public partial class SeedAppraisalPipelineActionSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kind = 1 (Action) — these perform side effects, so the first failure halts the
            // pipeline. Version/Severity are omitted deliberately: both carry database defaults
            // (1 and 0) added by 20260418113650 and 20260606193944.

            // Non-UI entry (API + reappraisal): create the appraisal immediately at workflow start.
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM workflow.ActivityProcessConfigurations
                    WHERE ActivityName = '__on_workflow_start__'
                      AND ProcessorName = 'EmitAppraisalCreationRequested'
                )
                BEGIN
                    INSERT INTO workflow.ActivityProcessConfigurations
                        (Id, ActivityName, StepName, ProcessorName, Kind, SortOrder, ParametersJson,
                         IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                    VALUES
                        (NEWID(), '__on_workflow_start__', 'Emit appraisal creation (non-UI)',
                         'EmitAppraisalCreationRequested', 1, 1,
                         '{"condition": "entrySource != ''UI''"}',
                         1, GETDATE(), 'system', GETDATE(), 'system')
                END
                """);

            // UI entry: defer creation until appraisal-initiation-check is approved (decision 'P').
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM workflow.ActivityProcessConfigurations
                    WHERE ActivityName = 'appraisal-initiation-check'
                      AND ProcessorName = 'EmitAppraisalCreationRequested'
                )
                BEGIN
                    INSERT INTO workflow.ActivityProcessConfigurations
                        (Id, ActivityName, StepName, ProcessorName, Kind, SortOrder, ParametersJson,
                         IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                    VALUES
                        (NEWID(), 'appraisal-initiation-check', 'Emit appraisal creation (UI)',
                         'EmitAppraisalCreationRequested', 1, 1,
                         '{"condition": "entrySource == ''UI''", "requireDecision": "P"}',
                         1, GETDATE(), 'system', GETDATE(), 'system')
                END
                """);

            // Marks the appraisal as internally assigned on completion, which enables the
            // conditional "Proceed" action at appraisal-assignment on revisits.
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM workflow.ActivityProcessConfigurations
                    WHERE ActivityName = 'int-appraisal-execution'
                      AND ProcessorName = 'SetVariable'
                )
                BEGIN
                    INSERT INTO workflow.ActivityProcessConfigurations
                        (Id, ActivityName, StepName, ProcessorName, Kind, SortOrder, ParametersJson,
                         IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                    VALUES
                        (NEWID(), 'int-appraisal-execution', 'Set assignment type to Internal',
                         'SetVariable', 1, 1,
                         '{"variable": "assignmentType", "value": "Internal"}',
                         1, GETDATE(), 'system', GETDATE(), 'system')
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM workflow.ActivityProcessConfigurations
                WHERE ProcessorName = 'EmitAppraisalCreationRequested'
                  AND ActivityName IN ('__on_workflow_start__', 'appraisal-initiation-check')
                """);

            migrationBuilder.Sql("""
                DELETE FROM workflow.ActivityProcessConfigurations
                WHERE ActivityName = 'int-appraisal-execution'
                  AND ProcessorName = 'SetVariable'
                """);
        }
    }
}
