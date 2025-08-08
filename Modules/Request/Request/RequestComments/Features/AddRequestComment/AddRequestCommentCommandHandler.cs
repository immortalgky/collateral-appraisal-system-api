namespace Request.RequestComments.Features.AddRequestComment;

public class AddRequestCommentCommandHandler(
    IRequestRepository requestRepository,
    IRequestCommentRepository requestCommentRepository)
    : ICommandHandler<AddRequestCommentCommand, AddRequestCommentResult>
{
    public async Task<AddRequestCommentResult> Handle(AddRequestCommentCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            throw new RequestNotFoundException(command.RequestId);
        }

        var comment = RequestComment.Create(command.RequestId, command.Comment);

        await requestCommentRepository.AddAsync(comment, cancellationToken);

        await requestCommentRepository.SaveChangesAsync(cancellationToken);

        return new AddRequestCommentResult(true);
    }
}