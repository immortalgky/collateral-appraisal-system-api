using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleLand : RequestTitle
{
    public TitleDeedInfo TitleDeedInfo { get; private set; } = default!;
    public LandLocationInfo LandLocationInfo { get; private set; } = default!;
    public LandArea LandArea { get; private set; } = default!;

    private TitleLand()
    {
    }

    private TitleLand(RequestTitleData data) : base(data)
    {
        TitleDeedInfo = data.TitleDeedInfo;
        LandLocationInfo = data.LandLocationInfo;
        LandArea = data.LandArea;
    }

    public static TitleLand Create(RequestTitleData data) => new(data);

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        TitleDeedInfo = data.TitleDeedInfo;
        LandLocationInfo = data.LandLocationInfo;
        LandArea = data.LandArea;
    }

    public override void Validate()
    {
        base.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerName);

        LandArea.Validate();
        LandLocationInfo.Validate();
        TitleDeedInfo.Validate();
    }
}
