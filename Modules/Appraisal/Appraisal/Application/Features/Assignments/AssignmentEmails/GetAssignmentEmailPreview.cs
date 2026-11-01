namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <param name="FromEmailId">When set, the stored email of this case to show again (ส่งอีกครั้ง's pre-fill) instead of the template.</param>
public record GetAssignmentEmailPreviewQuery(Guid AppraisalId, Guid? FromEmailId = null) : IQuery<AssignmentEmailPreviewResult>;

/// <summary>
/// The email the admin edits when pressing มอบหมาย (EXT / EXTO): the bank template filled from the case's current
/// assignment row. Nothing is saved; the edited email travels with the assign request.
/// </summary>
public class GetAssignmentEmailPreviewQueryHandler(
    AssignmentEmailComposer composer,
    ICurrentUserService currentUser,
    ISender sender) : IQueryHandler<GetAssignmentEmailPreviewQuery, AssignmentEmailPreviewResult>
{
    public async Task<AssignmentEmailPreviewResult> Handle(GetAssignmentEmailPreviewQuery query, CancellationToken cancellationToken)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);
        if (query.FromEmailId is not { } emailId)
            return await composer.ComposeAsync(query.AppraisalId, cancellationToken);

        // The stored email through the history query: same access and 404 (it must belong to this appraisal).
        var stored = await sender.Send(new GetAssignmentEmailSentQuery(query.AppraisalId, emailId), cancellationToken);
        return await composer.ComposeFromAsync(
            query.AppraisalId,
            new StoredAssignmentEmail(stored.Subject, stored.BodyHtml, stored.ExtraTo, stored.Cc, stored.Attachments),
            cancellationToken);
    }
}
