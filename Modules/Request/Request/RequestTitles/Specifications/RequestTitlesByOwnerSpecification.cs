namespace Request.RequestTitles.Specifications;

public class RequestTitlesByOwnerSpecification : Specification<RequestTitle>
{
    private readonly string _owner;

    public RequestTitlesByOwnerSpecification(string owner)
    {
        _owner = owner;
    }

    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => title.Owner != null && title.Owner.Contains(_owner);
    }
}