namespace Request.RequestComments.Features.AddRequestComment;

public record AddRequestCommentCommand(Guid RequestId, string Comment, string CommentedBy, string CommentedByName) : ICommand<AddRequestCommentResult>;
