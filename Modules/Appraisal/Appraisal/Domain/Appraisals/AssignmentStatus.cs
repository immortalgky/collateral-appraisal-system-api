namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing assignment status.
/// Simplified flow: Assigned → InProgress → Completed (or Rejected/Cancelled)
/// </summary>
public class AssignmentStatus : ValueObject
{
    public string Code { get; }

    private AssignmentStatus(string code)
    {
        Code = code;
    }

    // Predefined statuses
    public static AssignmentStatus Pending => new("PENDING");
    public static AssignmentStatus Assigned => new("ASSIGNED");
    public static AssignmentStatus InProgress => new("IN_PROGRESS");
    public static AssignmentStatus Completed => new("COMPLETED");
    public static AssignmentStatus Rejected => new("REJECTED");
    public static AssignmentStatus Cancelled => new("CANCELLED");

    // Factory method from string
    public static AssignmentStatus FromString(string code)
    {
        return code switch
        {
            "PENDING" => Pending,
            "Pending" => Pending,
            "ASSIGNED" => Assigned,
            "Assigned" => Assigned,
            "IN_PROGRESS" => InProgress,
            "InProgress" => InProgress,
            "COMPLETED" => Completed,
            "Completed" => Completed,
            "REJECTED" => Rejected,
            "Rejected" => Rejected,
            "CANCELLED" => Cancelled,
            "Cancelled" => Cancelled,
            _ => throw new ArgumentException($"Invalid assignment status: {code}")
        };
    }

    // Implicit conversion to string
    public static implicit operator string(AssignmentStatus status)
    {
        return status.Code;
    }

    // Equality
    public static bool operator ==(AssignmentStatus? left, AssignmentStatus? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(AssignmentStatus? left, AssignmentStatus? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is AssignmentStatus other)
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