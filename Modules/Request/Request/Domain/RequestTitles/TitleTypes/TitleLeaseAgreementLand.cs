using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleLeaseAgreementLand : RequestTitle
{
    public TitleDeedInfo TitleDeedInfo { get; private set; } = default!;
    public LandLocationInfo LandLocationInfo { get; private set; } = default!;
    public LandArea LandArea { get; private set; } = default!;

    private TitleLeaseAgreementLand()
    {
    }

    private TitleLeaseAgreementLand(RequestTitleData data) : base(data)
    {
        TitleDeedInfo = data.TitleDeedInfo;
        LandLocationInfo = data.LandLocationInfo;
        LandArea = data.LandArea;
    }

    public static TitleLeaseAgreementLand Create(RequestTitleData data) => new(data);

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

        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(OwnerName), "ownerName is required.");
        ruleCheck.ThrowIfInvalid();

        LandArea.Validate();
        LandLocationInfo.Validate();
        TitleDeedInfo.Validate();
    }
}
