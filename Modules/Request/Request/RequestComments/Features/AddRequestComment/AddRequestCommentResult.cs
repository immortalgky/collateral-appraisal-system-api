namespace Request.RequestComments.Features.AddRequestComment;

public record AddRequestCommentResult
{
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public string? Comment { get; init; }
    public string? CommentedBy { get; init; }
    public string? CommentedByName { get; init; }
    public DateTime CommentedAt { get; init; }
};