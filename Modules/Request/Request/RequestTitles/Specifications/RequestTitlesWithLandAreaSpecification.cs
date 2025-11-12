namespace Request.RequestTitles.Specifications;

public class RequestTitlesWithLandAreaSpecification : Specification<RequestTitle>
{
    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => title.LandArea.AreaRai > 0 || title.LandArea.AreaNgan > 0 || title.LandArea.AreaSquareWa > 0;
    }
}

public class RequestTitlesWithMinimumLandAreaSpecification : Specification<RequestTitle>
{
    private readonly decimal _minimumWa;

    public RequestTitlesWithMinimumLandAreaSpecification(decimal minimumWa)
    {
        _minimumWa = minimumWa;
    }

    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => (title.LandArea.AreaRai * 400 + title.LandArea.AreaNgan * 100 + title.LandArea.AreaSquareWa) >= _minimumWa;
    }
}