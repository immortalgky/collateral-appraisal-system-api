using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleLeaseAgreementCondo : RequestTitle
{
    public TitleDeedInfo TitleDeedInfo { get; private set; } = default!;
    public CondoInfo CondoInfo { get; private set; } = default!;

    private TitleLeaseAgreementCondo()
    {
    }

    private TitleLeaseAgreementCondo(RequestTitleData data) : base(data)
    {
        TitleDeedInfo = data.TitleDeedInfo;
        CondoInfo = data.CondoInfo;
    }

    public static TitleLeaseAgreementCondo Create(RequestTitleData data) => new(data);

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        TitleDeedInfo = data.TitleDeedInfo;
        CondoInfo = data.CondoInfo;
    }

    public override void Validate()
    {
        base.Validate();

        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(OwnerName), "ownerName is required.");
        ruleCheck.ThrowIfInvalid();

        TitleDeedInfo.Validate();
        CondoInfo.Validate();
    }
}
