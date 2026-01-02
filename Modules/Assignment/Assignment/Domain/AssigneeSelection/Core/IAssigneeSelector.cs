namespace Assignment.Domain.AssigneeSelection.Core;

public interface IAssigneeSelector
{
    Task<AssigneeSelectionResult> SelectAssigneeAsync(AssignmentContext context, CancellationToken cancellationToken = default);
}