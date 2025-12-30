namespace Request.Domain.Requests;

public class Collateral : ValueObject
{
    private Collateral(
        string? collateralType,
        string collateralStatus,
        string? titleNo,
        string? owner,
        int? noOfBuilding,
        string? titleDetail
    )
    {
        CollateralType = collateralType;
        CollateralStatus = collateralStatus;
        TitleNo = titleNo;
        Owner = owner;
        NoOfBuilding = noOfBuilding;
        TitleDetail = titleDetail;
    }
    public string? CollateralType { get; } = default!;
    public string CollateralStatus { get; } = default!;
    public string? TitleNo { get; } = default!;
    public string? Owner { get; } = default!;
    public int? NoOfBuilding { get; }
    public string? TitleDetail { get; } = default!;

    public static Collateral Create(
        string? collateralType,
        string collateralStatus,
        string? titleNo,
        string? owner,
        int? noOfBuilding,
        string? titleDetail
    )
    {
        return new Collateral(collateralType, collateralStatus, titleNo, owner, noOfBuilding, titleDetail);
    }
}