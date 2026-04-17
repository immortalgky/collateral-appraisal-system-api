using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations;

/// <summary>
/// Seeds ActivityProcessConfiguration for int-appraisal-execution to set
/// assignmentType = 'Internal' on completion, enabling the conditional
/// "Proceed" action at appraisal-assignment on revisits.
/// </summary>
public partial class SeedSetAssignmentTypeStep : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM workflow.ActivityProcessConfigurations
                WHERE ActivityName = 'int-appraisal-execution'
                  AND ProcessorName = 'SetVariable'
            )
            BEGIN
                INSERT INTO workflow.ActivityProcessConfigurations
                    (Id, ActivityName, StepName, ProcessorName, SortOrder, Parameters, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                VALUES
                    (NEWID(), 'int-appraisal-execution', 'Set assignment type to Internal', 'SetVariable', 1,
                     '{"variable": "assignmentType", "value": "Internal"}', 1, GETUTCDATE(), 'system', GETUTCDATE(), 'system')
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM workflow.ActivityProcessConfigurations
            WHERE ActivityName = 'int-appraisal-execution'
              AND ProcessorName = 'SetVariable'
            """);
    }
}
