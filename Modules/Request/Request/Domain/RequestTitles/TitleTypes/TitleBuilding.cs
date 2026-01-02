using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleBuilding : RequestTitle
{
    public BuildingInfo BuildingInfo { get; private set; } = default!;

    private TitleBuilding()
    {
    }

    private TitleBuilding(RequestTitleData data) : base(data)
    {
        BuildingInfo = data.BuildingInfo;
    }

    public static TitleBuilding Create(RequestTitleData data) => new(data);

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
