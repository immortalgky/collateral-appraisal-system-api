namespace Request.Contracts.Requests.Dtos;

public record RequestCommentDto
{
    public Guid? Id { get; init; }
    public Guid RequestId { get; init; }
    public string Comment { get; init; } = default!;
    public string CommentedBy { get; init; } = default!;
    public string CommentedByName { get; init; } = default!;
    public DateTime? CommentedAt { get; init; }
};
