namespace Request.Requests.Specifications;

public class RequestsByPurposeSpecification : Specification<Requests.Models.Request>
{
    private readonly string _purpose;

    public RequestsByPurposeSpecification(string purpose)
    {
        _purpose = purpose;
    }

    public override Expression<Func<Requests.Models.Request, bool>> ToExpression()
    {
        return request => request.Detail.Purpose.Contains(_purpose);
    }
}