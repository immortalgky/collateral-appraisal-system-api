namespace Request.Application.Features.RequestComments.AddRequestComment;

public record AddRequestCommentCommand(
    Guid RequestId,
    string Comment,
    string CommentedBy,
    string CommentedByName
) : ICommand<AddRequestCommentResult>, ITransactionalCommand<IRequestUnitOfWork>;