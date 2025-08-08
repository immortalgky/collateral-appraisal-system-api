namespace Request.RequestComments.Specifications;

public class RequestCommentsByRequestIdSpecification : Specification<RequestComment>
{
    private readonly long _requestId;

    public RequestCommentsByRequestIdSpecification(long requestId)
    {
        _requestId = requestId;
    }

    public override Expression<Func<RequestComment, bool>> ToExpression()
    {
        return comment => comment.RequestId == _requestId;
    }
}