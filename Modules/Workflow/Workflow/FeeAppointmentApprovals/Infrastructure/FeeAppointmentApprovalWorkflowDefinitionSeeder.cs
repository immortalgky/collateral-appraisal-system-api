using Shared.Data.Seed;
using Workflow.Workflow.Infrastructure.Seed;

namespace Workflow.FeeAppointmentApprovals.Infrastructure;

/// <summary>
/// Seeds the fee-appointment approval workflow definition, plus its Published v1 version, from the
/// embedded JSON resource. Idempotent: skipped if a definition with the same name already exists.
/// </summary>
public class FeeAppointmentApprovalWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<FeeAppointmentApprovalWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public const string WorkflowName = "Fee Appointment Approval Workflow";

    public Task SeedAllAsync()
    {
        var json = WorkflowDefinitionSeedHelper.LoadEmbeddedResource(
            typeof(FeeAppointmentApprovalWorkflowDefinitionSeeder).Assembly,
            "Workflow.Workflow.Config.fee-appointment-approval-workflow.json",
            "Workflow.Workflow.Config.fee_appointment_approval_workflow.json");

        return WorkflowDefinitionSeedHelper.SeedAsync(
            context,
            logger,
            name: WorkflowName,
            description: "Approval workflow for external company fee and appointment changes",
            category: "Appraisal",
            createdBy: "system",
            fileJson: json);
    }
}
