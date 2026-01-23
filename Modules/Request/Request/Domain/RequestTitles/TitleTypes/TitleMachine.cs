using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleMachine : RequestTitle
{
    public MachineInfo MachineInfo { get; private set; } = default!;

    private TitleMachine()
    {
    }

    private TitleMachine(RequestTitleData data) : base(data)
    {
        MachineInfo = data.MachineInfo;
    }

    public static TitleMachine Create(RequestTitleData data)
    {
        return new TitleMachine(data);
    }

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        MachineInfo = data.MachineInfo;
    }

    public override void Validate()
    {
        base.Validate();

        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(MachineInfo.RegistrationNumber), "registrationNo is required.");
        ruleCheck.ThrowIfInvalid();

        MachineInfo.Validate();
    }
}