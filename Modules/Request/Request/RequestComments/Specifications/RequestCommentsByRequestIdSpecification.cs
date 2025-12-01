namespace Request.RequestComments.Specifications;

public class RequestCommentsByRequestIdSpecification : Specification<RequestComment>
{
    private readonly Guid _requestId;

    public RequestCommentsByRequestIdSpecification(Guid requestId)
    {
        _requestId = requestId;
    }

    public override Expression<Func<RequestComment, bool>> ToExpression()
    {
        return comment => comment.RequestId == _requestId;
    }
}