namespace Request.Application.Features.RequestComments.AddRequestComment;

public record AddRequestCommentRequest(
    string Comment, 
    string CommentedBy, 
    string CommentedByName
);


