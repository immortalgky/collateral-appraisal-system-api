namespace Request.RequestComments.Features.AddRequestComment;

public record AddRequestCommentCommand(Guid RequestId, string Comment) : ICommand<AddRequestCommentResult>;