namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingStatus : ValueObject
{
    public string Code { get; }
    private SupportingStatus(string code) => Code = code;

    public static SupportingStatus PendingApproval => new("PendingApproval");
    public static SupportingStatus Approved        => new("Approved");
    public static SupportingStatus Rejected        => new("Rejected");
    public static SupportingStatus Archived        => new("Archived");

    public static SupportingStatus FromString(string code) => code switch
    {
        "PendingApproval" => PendingApproval,
        "Approved"        => Approved,
        "Rejected"        => Rejected,
        "Archived"        => Archived,
        _ => throw new ArgumentException($"Invalid supporting status: {code}")
    };

    public override string ToString() => Code;
    public static implicit operator string(SupportingStatus s) => s.Code;
}