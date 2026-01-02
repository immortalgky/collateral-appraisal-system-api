using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleCondo : RequestTitle
{
    public TitleDeedInfo TitleDeedInfo { get; private set; } = default!;
    public CondoInfo CondoInfo { get; private set; } = default!;

    private TitleCondo()
    {
    }

    private TitleCondo(RequestTitleData data) : base(data)
    {
        TitleDeedInfo = data.TitleDeedInfo;
        CondoInfo = data.CondoInfo;
    }

    public static TitleCondo Create(RequestTitleData data) => new(data);

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        TitleDeedInfo = data.TitleDeedInfo;
        CondoInfo = data.CondoInfo;
    }

    public override void Validate()
    {
        base.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerName);

        TitleDeedInfo.Validate();
        CondoInfo.Validate();
    }
}
