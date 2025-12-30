namespace Request.Domain.RequestComments;

public class RequestComment : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public string Comment { get; private set; } = default!;
    public string CommentedBy { get; private set; } = default!;
    public string CommentedByName { get; private set; } = default!;
    public DateTime CommentedAt { get; private set; }
    public DateTime LastModifiedAt { get; private set; }

    private RequestComment(Guid id, Guid requestId, DateTime commentedAt)
    {
        Id = id;
        RequestId = requestId;
        CommentedAt = commentedAt;

        AddDomainEvent(new RequestCommentAddedEvent(requestId, this));
    }

    public static RequestComment Create(RequestCommentData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Comment);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CommentedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CommentedByName);

        return new RequestComment(Guid.NewGuid(), data.RequestId, data.CommentedAt)
        {
            Comment = data.Comment,
            CommentedBy = data.CommentedBy,
            CommentedByName = data.CommentedByName
        };
    }

    public void Update(string comment, DateTime lastModifiedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);

        var beforeModified = Comment;

        Comment = comment;
        LastModifiedAt = lastModifiedAt;

        AddDomainEvent(new RequestCommentUpdatedEvent(RequestId, this, beforeModified));
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Comment);
        ArgumentException.ThrowIfNullOrWhiteSpace(CommentedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(CommentedByName);
    }
}

public record RequestCommentData(
    Guid RequestId,
    string Comment,
    string CommentedBy,
    string CommentedByName,
    DateTime CommentedAt
);