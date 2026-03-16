namespace Workflow.AssigneeSelection.Pipeline;

public class ExclusionRuleValidator : IAssignmentValidator
{
    private readonly ILogger<ExclusionRuleValidator> _logger;

    public ExclusionRuleValidator(ILogger<ExclusionRuleValidator> logger)
    {
        _logger = logger;
    }

    public Task<AssignmentValidationResult> ValidateAsync(
        AssignmentPipelineContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.SelectedAssignee) || context.Rules.ExcludeAssigneesFrom.Count == 0)
            return Task.FromResult(AssignmentValidationResult.Valid());

        foreach (var sourceActivityId in context.Rules.ExcludeAssigneesFrom)
        {
            if (context.PriorAssignees.TryGetValue(sourceActivityId, out var excludedUserId)
                && excludedUserId == context.SelectedAssignee)
            {
                _logger.LogWarning(
                    "Assignee {Assignee} was excluded because they completed {SourceActivity}",
                    context.SelectedAssignee, sourceActivityId);

                return Task.FromResult(AssignmentValidationResult.Invalid(
                    $"Assignee '{context.SelectedAssignee}' is excluded because they were the assignee of '{sourceActivityId}'"));
            }
        }

        return Task.FromResult(AssignmentValidationResult.Valid());
    }
}
