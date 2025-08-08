namespace Request.Requests.Features.RemoveCommentFromRequest;

public class RemoveCommentFromRequestHandler(RequestDbContext dbContext)
    : ICommandHandler<RemoveCommentFromRequestCommand, RemoveCommentFromRequestResult>
{
    public async Task<RemoveCommentFromRequestResult> Handle(RemoveCommentFromRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([command.Id], cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.Id);
        }

        request.RemoveComment(command.CommentId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RemoveCommentFromRequestResult(true);
    }
}