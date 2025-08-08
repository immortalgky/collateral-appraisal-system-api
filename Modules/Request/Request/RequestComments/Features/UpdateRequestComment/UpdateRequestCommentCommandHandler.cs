namespace Request.RequestComments.Features.UpdateRequestComment;

public class UpdateRequestCommentCommandHandler(IRequestCommentRepository requestCommentRepository)
    : ICommandHandler<UpdateRequestCommentCommand, UpdateRequestCommentResult>
{
    public async Task<UpdateRequestCommentResult> Handle(UpdateRequestCommentCommand command,
        CancellationToken cancellationToken)
    {
        var comment = await requestCommentRepository.GetByIdAsync(command.CommentId, cancellationToken);
        if (comment is null)
        {
            throw new DomainException($"Comment with ID {command.CommentId} not found.");
        }

        comment.Update(command.Comment);

        await requestCommentRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestCommentResult(true);
    }
}