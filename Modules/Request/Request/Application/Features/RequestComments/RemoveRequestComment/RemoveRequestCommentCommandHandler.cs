using Shared.Identity;

namespace Request.Application.Features.RequestComments.RemoveRequestComment;

public class RemoveRequestCommentCommandHandler(
    IRequestCommentRepository requestCommentRepository,
    ICurrentUserService currentUserService
) : ICommandHandler<RemoveRequestCommentCommand, RemoveRequestCommentResult>
{
    public async Task<RemoveRequestCommentResult> Handle(RemoveRequestCommentCommand command,
        CancellationToken cancellationToken)
    {
        var comment = await requestCommentRepository.GetByIdAsync(command.CommentId, cancellationToken);
        if (comment is null) throw new DomainException($"Comment with ID {command.CommentId} not found.");

        // Publish domain event before removal
        comment.AddDomainEvent(new RequestCommentRemovedEvent(comment.RequestId, command.CommentId, comment.Comment,
            currentUserService.Username ?? "anonymous"));

        await requestCommentRepository.DeleteAsync(comment, cancellationToken);

        return new RemoveRequestCommentResult(true);
    }
}