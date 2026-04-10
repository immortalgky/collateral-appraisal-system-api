namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing the status of an appraisal
/// </summary>
public class AppraisalStatus : ValueObject
{
    public string Code { get; }

    private AppraisalStatus(string code)
    {
        Code = code;
    }

    // Predefined statuses
    public static AppraisalStatus Pending => new("PENDING");
    public static AppraisalStatus Assigned => new("ASSIGNED");
    public static AppraisalStatus InProgress => new("IN_PROGRESS");
    public static AppraisalStatus UnderReview => new("UNDER_REVIEW");
    public static AppraisalStatus Completed => new("COMPLETED");
    public static AppraisalStatus Cancelled => new("CANCELLED");

    // Factory method from string
    public static AppraisalStatus FromString(string code)
    {
        return code switch
        {
            "PENDING" => Pending,
            "Pending" => Pending,
            "ASSIGNED" => Assigned,
            "Assigned" => Assigned,
            "IN_PROGRESS" => InProgress,
            "InProgress" => InProgress,
            "UNDER_REVIEW" => UnderReview,
            "UnderReview" => UnderReview,
            "COMPLETED" => Completed,
            "Completed" => Completed,
            "CANCELLED" => Cancelled,
            "Cancelled" => Cancelled,
            _ => throw new ArgumentException($"Invalid appraisal status: {code}")
        };
    }

    // Implicit conversion to string
    public static implicit operator string(AppraisalStatus status)
    {
        return status.Code;
    }

    // Equality
    public static bool operator ==(AppraisalStatus? left, AppraisalStatus? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(AppraisalStatus? left, AppraisalStatus? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is AppraisalStatus other)
            return Code == other.Code;
        return false;
    }

    public override int GetHashCode()
    {
        return Code.GetHashCode();
    }

    public override string ToString()
    {
        return Code;
    }
}