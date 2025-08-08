namespace Request.RequestTitles.Specifications;

public class RequestTitlesWithLandAreaSpecification : Specification<RequestTitle>
{
    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => title.LandArea.Rai > 0 || title.LandArea.Ngan > 0 || title.LandArea.Wa > 0;
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
        return title => (title.LandArea.Rai * 400 + title.LandArea.Ngan * 100 + title.LandArea.Wa) >= _minimumWa;
    }
}