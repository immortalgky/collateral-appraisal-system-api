namespace Appraisal.Domain.Appraisals;

public class SlaStatus : ValueObject
{
    public string Code { get; }

    private SlaStatus(string code)
    {
        Code = code;
    }

    public static SlaStatus OnTime  => new("ON_TIME");
    public static SlaStatus OnTrack => new("ON_TRACK");
    public static SlaStatus AtRisk  => new("AT_RISK");
    public static SlaStatus Breached => new("BREACHED");

    public static SlaStatus FromString(string code)
    {
        return code switch
        {
            "ON_TIME"  => OnTime,
            "ON_TRACK" => OnTrack,
            "AT_RISK"  => AtRisk,
            "BREACHED" => Breached,
            _ => throw new ArgumentException($"Invalid SLA status: {code}")
        };
    }

    public static implicit operator string(SlaStatus status) => status.Code;

    public override string ToString() => Code;
}
