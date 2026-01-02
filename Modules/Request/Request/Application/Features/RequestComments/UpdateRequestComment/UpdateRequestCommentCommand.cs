namespace Request.Application.Features.RequestComments.UpdateRequestComment;

public record UpdateRequestCommentCommand(Guid CommentId, string Comment)
    : ICommand<UpdateRequestCommentResult>, ITransactionalCommand<IRequestUnitOfWork>;