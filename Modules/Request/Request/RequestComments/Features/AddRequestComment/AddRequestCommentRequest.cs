namespace Request.RequestComments.Features.AddRequestComment;

public record AddRequestCommentRequest(string Comment, string CommentedBy, string CommentedByName);