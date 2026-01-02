using Request.Domain.RequestTitles;

namespace Request.Domain.RequestTitles.TitleTypes;

public sealed class TitleVehicle : RequestTitle
{
    public VehicleInfo VehicleInfo { get; private set; } = default!;

    private TitleVehicle()
    {
    }

    private TitleVehicle(RequestTitleData data) : base(data)
    {
        VehicleInfo = data.VehicleInfo;
    }

    public static TitleVehicle Create(RequestTitleData data) => new(data);

    public override void Update(RequestTitleData data)
    {
        base.Update(data);
        VehicleInfo = data.VehicleInfo;
    }

    public override void Validate()
    {
        base.Validate();

        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(VehicleInfo.VIN), "VIN is required.");
        ruleCheck.ThrowIfInvalid();

        VehicleInfo.Validate();
    }
}
