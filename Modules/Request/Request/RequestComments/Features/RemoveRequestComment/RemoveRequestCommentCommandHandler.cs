namespace Request.RequestComments.Features.RemoveRequestComment;

public class RemoveRequestCommentCommandHandler(IRequestCommentRepository requestCommentRepository)
    : ICommandHandler<RemoveRequestCommentCommand, RemoveRequestCommentResult>
{
    public async Task<RemoveRequestCommentResult> Handle(RemoveRequestCommentCommand command,
        CancellationToken cancellationToken)
    {
        var comment = await requestCommentRepository.GetByIdAsync(command.CommentId, cancellationToken);
        if (comment is null)
        {
            throw new DomainException($"Comment with ID {command.CommentId} not found.");
        }

        // Publish domain event before removal
        comment.AddDomainEvent(new RequestCommentRemovedEvent(comment.RequestId, command.CommentId, comment.Comment,
            comment.UpdatedBy));

        requestCommentRepository.Remove(comment, cancellationToken);

        await requestCommentRepository.SaveChangesAsync(cancellationToken);

        return new RemoveRequestCommentResult(true);
    }
}