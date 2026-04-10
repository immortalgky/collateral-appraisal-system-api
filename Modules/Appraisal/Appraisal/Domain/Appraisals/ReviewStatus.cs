namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value Object representing review status.
/// Simplified: Pending -> Approved / Returned (no Rejected)
/// </summary>
public class ReviewStatus : ValueObject
{
    public string Code { get; private set; } = null!;

    public static readonly ReviewStatus Pending = new("PENDING");
    public static readonly ReviewStatus Approved = new("APPROVED");
    public static readonly ReviewStatus Returned = new("RETURNED");

    private ReviewStatus()
    {
    }

    private ReviewStatus(string code)
    {
        Code = code;
    }

    public static ReviewStatus FromString(string code)
    {
        return code switch
        {
            "PENDING" => Pending,
            "Pending" => Pending,
            "APPROVED" => Approved,
            "Approved" => Approved,
            "RETURNED" => Returned,
            "Returned" => Returned,
            _ => throw new ArgumentException($"Invalid review status: {code}")
        };
    }

    public override string ToString()
    {
        return Code;
    }

    public static implicit operator string(ReviewStatus status)
    {
        return status.Code;
    }
}