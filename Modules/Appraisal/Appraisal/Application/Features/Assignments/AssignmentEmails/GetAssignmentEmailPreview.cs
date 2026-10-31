namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

public record GetAssignmentEmailPreviewQuery(Guid AppraisalId) : IQuery<AssignmentEmailPreviewResult>;

/// <summary>
/// The email the admin edits when pressing มอบหมาย (EXT / EXTO): the bank template filled from the case's current
/// assignment row. Nothing is saved; the edited email travels with the assign request.
/// </summary>
public class GetAssignmentEmailPreviewQueryHandler(
    AssignmentEmailComposer composer,
    ICurrentUserService currentUser) : IQueryHandler<GetAssignmentEmailPreviewQuery, AssignmentEmailPreviewResult>
{
    public Task<AssignmentEmailPreviewResult> Handle(GetAssignmentEmailPreviewQuery query, CancellationToken cancellationToken)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);
        return composer.ComposeAsync(query.AppraisalId, cancellationToken);
    }
}
