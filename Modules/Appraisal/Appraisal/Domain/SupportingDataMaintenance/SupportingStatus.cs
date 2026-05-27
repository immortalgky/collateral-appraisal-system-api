namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingStatus : ValueObject
{
    public string Code { get; }
    private SupportingStatus(string code) => Code = code;

    public static SupportingStatus Draft => new("Draft");
    public static SupportingStatus Pending => new("Pending");
    public static SupportingStatus Approved => new("Approved");
    public static SupportingStatus Rejected => new("Rejected");
    public static SupportingStatus Cancelled => new("Cancelled");
    public static SupportingStatus RoutedBack => new("RoutedBack");

    public static SupportingStatus FromString(string code) => code switch
    {
        "Draft" => Draft,
        "Pending" => Pending,
        "Approved" => Approved,
        "Rejected" => Rejected,
        "Cancelled" => Cancelled,
        "RoutedBack" => RoutedBack,
        _ => throw new ArgumentException($"Invalid supporting status: {code}")
    };

    public override string ToString() => Code;
    public static implicit operator string(SupportingStatus s) => s.Code;
}