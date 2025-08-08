namespace Request.Requests.Features.UpdateRequestComment;

public class UpdateRequestCommentHandler(RequestDbContext dbContext)
    : ICommandHandler<UpdateRequestCommentCommand, UpdateRequestCommentResult>
{
    public async Task<UpdateRequestCommentResult> Handle(UpdateRequestCommentCommand command,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([command.Id], cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.Id);
        }

        request.UpdateComment(command.CommentId, command.Comment);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateRequestCommentResult(true);
    }
}