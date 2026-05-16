using Workflow.Contracts.DocumentFollowups;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

/// <summary>
/// Resumes the parent "Collateral Appraisal Workflow" instance for a request after a
/// data-fix resubmit. CorrelationId = requestId.ToString() per the workflow start convention
/// in RequestSubmittedIntegrationEventConsumer.
///
/// If the workflow is not found or is not at the expected activity, this command logs a warning
/// and returns Unit without throwing — the document/title sync has already committed.
/// </summary>
public class ResumeParentWorkflowForRequestCommandHandler(
    IWorkflowInstanceRepository workflowInstanceRepository,
    IWorkflowService workflowService,
    ILogger<ResumeParentWorkflowForRequestCommandHandler> logger
) : ICommandHandler<ResumeParentWorkflowForRequestCommand, Unit>
{
    private const string AppraisalWorkflowName = "Collateral Appraisal Workflow";
    private const string ExpectedActivityId = "appraisal-initiation";

    public async Task<Unit> Handle(ResumeParentWorkflowForRequestCommand command, CancellationToken cancellationToken)
    {
        var instance = await workflowInstanceRepository.GetByCorrelationIdAsync(
            command.RequestId.ToString(),
            AppraisalWorkflowName,
            cancellationToken);

        if (instance is null)
        {
            logger.LogWarning(
                "ResumeParentWorkflow: no running workflow found for RequestId {RequestId}. Skipping resume.",
                command.RequestId);
            return Unit.Value;
        }

        if (!string.Equals(instance.CurrentActivityId, ExpectedActivityId, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "ResumeParentWorkflow: workflow {WorkflowInstanceId} for RequestId {RequestId} is at activity " +
                "'{CurrentActivityId}', expected '{ExpectedActivityId}'. Skipping resume.",
                instance.Id, command.RequestId, instance.CurrentActivityId, ExpectedActivityId);
            return Unit.Value;
        }

        await workflowService.ResumeWorkflowAsync(
            instance.Id,
            instance.CurrentActivityId,
            command.Actor,
            new Dictionary<string, object> { ["decisionTaken"] = "P" },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "ResumeParentWorkflow: resumed workflow {WorkflowInstanceId} for RequestId {RequestId} at activity '{ActivityId}'",
            instance.Id, command.RequestId, instance.CurrentActivityId);

        return Unit.Value;
    }
}
