namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value Object representing review status.
/// Simplified: Pending -> Approved / Returned (no Rejected)
/// </summary>
public class ReviewStatus : ValueObject
{
    public string Code { get; private set; } = null!;

    public static readonly ReviewStatus Pending = new("Pending");
    public static readonly ReviewStatus Approved = new("Approved");
    public static readonly ReviewStatus Returned = new("Returned");

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
            "Pending" => Pending,
            "Approved" => Approved,
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