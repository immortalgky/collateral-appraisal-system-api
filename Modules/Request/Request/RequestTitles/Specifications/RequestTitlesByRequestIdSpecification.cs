namespace Request.RequestTitles.Specifications;

public class RequestTitlesByRequestIdSpecification : Specification<RequestTitle>
{
    private readonly Guid _requestId;

    public RequestTitlesByRequestIdSpecification(Guid requestId)
    {
        _requestId = requestId;
    }

    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => title.RequestId == _requestId;
    }
}