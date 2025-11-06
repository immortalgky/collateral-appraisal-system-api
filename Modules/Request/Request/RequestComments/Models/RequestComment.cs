namespace Request.RequestComments.Models;

public class RequestComment : Aggregate<long>
{
    public Guid RequestId { get; private set; }
    public string Comment { get; private set; }
    public string CommentedBy { get; private set; }
    public string CommentedOn { get; private set; }
    // UpdatedkBy
    // UpdatedOn

    private RequestComment(Guid requestId, string comment)
    {
        RequestId = requestId;
        Comment = comment;
    }

    public static RequestComment Create(Guid requestId, string comment)
    {
        var requestComment = new RequestComment(requestId, comment);
        requestComment.AddDomainEvent(new RequestCommentAddedEvent(requestId, requestComment));
        return requestComment;
    }

    public void Update(string comment)
    {
        var previousComment = Comment;
        Comment = comment;
        AddDomainEvent(new RequestCommentUpdatedEvent(RequestId, this, previousComment));
    }
}