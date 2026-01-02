using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleVessel : RequestTitle
{
    public VesselInfo VesselInfo { get; private set; } = default!;

    private TitleVessel()
    {
    }

    private TitleVessel(RequestTitleData data) : base(data)
    {
        VesselInfo = data.VesselInfo;
    }

    public static TitleVessel Create(RequestTitleData data) => new(data);

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        VesselInfo = data.VesselInfo;
    }

    public override void Validate()
    {
        base.Validate();

        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(VesselInfo.HullIdentificationNumber), "hullIdentificationNumber is required.");
        ruleCheck.ThrowIfInvalid();

        VesselInfo.Validate();
    }
}
