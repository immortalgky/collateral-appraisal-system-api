namespace Request.Requests.Specifications;

public class RequestsByPrioritySpecification : Specification<Requests.Models.Request>
{
    private readonly string _priority;

    public RequestsByPrioritySpecification(string priority)
    {
        _priority = priority;
    }

    public override Expression<Func<Requests.Models.Request, bool>> ToExpression()
    {
        return request => request.Detail.Priority == _priority;
    }
}