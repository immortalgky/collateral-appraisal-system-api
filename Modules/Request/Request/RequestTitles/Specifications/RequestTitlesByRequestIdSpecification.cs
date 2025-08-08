namespace Request.RequestTitles.Specifications;

public class RequestTitlesByRequestIdSpecification : Specification<RequestTitle>
{
    private readonly long _requestId;

    public RequestTitlesByRequestIdSpecification(long requestId)
    {
        _requestId = requestId;
    }

    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => title.RequestId == _requestId;
    }
}