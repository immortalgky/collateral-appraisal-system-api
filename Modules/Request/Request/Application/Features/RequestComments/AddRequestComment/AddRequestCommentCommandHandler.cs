namespace Request.Application.Features.RequestComments.AddRequestComment;

public class AddRequestCommentCommandHandler(
    IRequestRepository requestRepository,
    IRequestCommentRepository requestCommentRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<AddRequestCommentCommand, AddRequestCommentResult>
{
    public async Task<AddRequestCommentResult> Handle(AddRequestCommentCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.RequestId);

        var comment = RequestComment.Create(new RequestCommentData(
            command.RequestId,
            command.Comment,
            command.CommentedBy,
            command.CommentedByName,
            dateTimeProvider.Now
        ));

        await requestCommentRepository.AddAsync(comment, cancellationToken);

        return new AddRequestCommentResult(true);
    }
}