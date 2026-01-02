using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleLeaseAgreementBuilding : RequestTitle
{
    public BuildingInfo BuildingInfo { get; private set; } = default!;

    private TitleLeaseAgreementBuilding()
    {
    }

    private TitleLeaseAgreementBuilding(RequestTitleData data) : base(data)
    {
        BuildingInfo = data.BuildingInfo;
    }

    public static TitleLeaseAgreementBuilding Create(RequestTitleData data) => new(data);

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        BuildingInfo = data.BuildingInfo;
    }

    public override void Validate()
    {
        base.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerName);

        BuildingInfo.Validate();
    }
}
