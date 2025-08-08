using Request.RequestTitles.ValueObjects;

namespace Request.RequestTitles.Models;

public class RequestTitle : Aggregate<long>
{
    public long RequestId { get; private set; }
    public string CollateralType { get; private set; } = default!;
    public string? TitleNo { get; private set; }
    public string? TitleDetail { get; private set; }
    public string? Owner { get; private set; }
    public LandArea LandArea { get; private set; } = default!;
    public string? BuildingType { get; private set; }
    public decimal? UsageArea { get; private set; }
    public int? NoOfBuilding { get; private set; }
    public Address TitleAddress { get; private set; } = default!;
    public Address DopaAddress { get; private set; } = default!;
    public Vehicle Vehicle { get; private set; } = default!;
    public Machine Machine { get; private set; } = default!;

    private RequestTitle()
    {
        // For EF Core
    }

    public static RequestTitle Create(
        long requestId,
        string collateralType,
        string? titleNo,
        string? titleDetail,
        string? owner,
        LandArea landArea,
        string? buildingType,
        decimal? usageArea,
        int? noOfBuilding,
        Address titleAddress,
        Address dopaAddress,
        Vehicle vehicle,
        Machine machine
    )
    {
        var requestTitle = new RequestTitle
        {
            RequestId = requestId,
            CollateralType = collateralType,
            TitleNo = titleNo,
            TitleDetail = titleDetail,
            Owner = owner,
            LandArea = landArea,
            BuildingType = buildingType,
            UsageArea = usageArea,
            NoOfBuilding = noOfBuilding,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Vehicle = vehicle,
            Machine = machine
        };

        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestId, requestTitle));

        return requestTitle;
    }

    public void UpdateDetails(
        string collateralType,
        string? titleNo,
        string? titleDetail,
        string? owner,
        LandArea landArea,
        string? buildingType,
        decimal? usageArea,
        int? noOfBuilding,
        Address titleAddress,
        Address dopaAddress,
        Vehicle vehicle,
        Machine machine
    )
    {
        CollateralType = collateralType;
        TitleNo = titleNo;
        TitleDetail = titleDetail;
        Owner = owner;
        LandArea = landArea;
        BuildingType = buildingType;
        UsageArea = usageArea;
        NoOfBuilding = noOfBuilding;
        TitleAddress = titleAddress;
        DopaAddress = dopaAddress;
        Vehicle = vehicle;
        Machine = machine;
    }
}