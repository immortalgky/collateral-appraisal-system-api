namespace Request.RequestTitles.Specifications;

public class RequestTitlesByCollateralTypeSpecification : Specification<RequestTitle>
{
    private readonly string _collateralType;

    public RequestTitlesByCollateralTypeSpecification(string collateralType)
    {
        _collateralType = collateralType;
    }

    public override Expression<Func<RequestTitle, bool>> ToExpression()
    {
        return title => title.CollateralType == _collateralType;
    }
}