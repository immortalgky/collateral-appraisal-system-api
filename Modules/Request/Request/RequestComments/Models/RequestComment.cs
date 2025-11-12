namespace Request.RequestComments.Models;

public class RequestComment : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public string Comment { get; private set; }
    public string CommentedBy { get; private set; }
    public string CommentedByName { get; private set; }
    public DateTime CommentedAt { get; private set; }

    public Requests.Models.Request Request { get; private set; }

    private RequestComment(Guid requestId, string comment, string commentedBy, string commentedByName)
    {
        Id = Guid.NewGuid();
        RequestId = requestId;
        Comment = comment;
        CommentedBy = commentedBy;
        CommentedByName = commentedByName;
        CommentedAt = DateTime.Now;
    }

    public static RequestComment Create(Guid requestId, string comment, string commentedBy, string commentedByName)
    {
        if (comment is null)
            throw new Exception("Comment must not be null.");
        
        var requestComment = new RequestComment(requestId, comment, commentedBy, commentedByName );

        requestComment.AddDomainEvent(new RequestCommentAddedEvent(requestId, requestComment));
        
        return requestComment;
    }

    public void Update(string comment)
    {
        if (comment is null)
            throw new Exception("Comment must not be null.");
        
        var previousComment = Comment;
        Comment = comment;
        CommentedAt = DateTime.Now;

        AddDomainEvent(new RequestCommentUpdatedEvent(RequestId, this, previousComment));
    }
}