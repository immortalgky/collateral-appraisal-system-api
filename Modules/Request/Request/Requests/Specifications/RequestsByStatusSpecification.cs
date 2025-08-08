namespace Request.Requests.Specifications;

public class RequestsByStatusSpecification : Specification<Requests.Models.Request>
{
    private readonly RequestStatus _status;

    public RequestsByStatusSpecification(RequestStatus status)
    {
        _status = status;
    }

    public override Expression<Func<Requests.Models.Request, bool>> ToExpression()
    {
        return request => request.Status == _status;
    }
}