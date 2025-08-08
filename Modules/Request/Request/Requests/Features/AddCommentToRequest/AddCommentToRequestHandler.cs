namespace Request.Requests.Features.AddCommentToRequest;

public class AddCommentToRequestHandler(RequestDbContext dbContext)
    : ICommandHandler<AddCommentToRequestCommand, AddCommentToRequestResult>
{
    public async Task<AddCommentToRequestResult> Handle(AddCommentToRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([command.Id], cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.Id);
        }

        request.AddComment(command.Comment);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddCommentToRequestResult(true);
    }
}