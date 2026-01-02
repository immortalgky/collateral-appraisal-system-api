namespace Request.Application.Features.RequestComments.RemoveRequestComment;

public record RemoveRequestCommentCommand(Guid CommentId)
    : ICommand<RemoveRequestCommentResult>, ITransactionalCommand<IRequestUnitOfWork>;