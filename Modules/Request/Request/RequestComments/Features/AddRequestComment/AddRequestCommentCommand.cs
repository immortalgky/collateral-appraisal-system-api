namespace Request.RequestComments.Features.AddRequestComment;

public record AddRequestCommentCommand(long RequestId, string Comment) : ICommand<AddRequestCommentResult>;