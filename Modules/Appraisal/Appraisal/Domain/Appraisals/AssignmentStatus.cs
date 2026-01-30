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
    public static AssignmentStatus Assigned => new("Assigned");
    public static AssignmentStatus InProgress => new("InProgress");
    public static AssignmentStatus Completed => new("Completed");
    public static AssignmentStatus Rejected => new("Rejected");
    public static AssignmentStatus Cancelled => new("Cancelled");

    // Factory method from string
    public static AssignmentStatus FromString(string code)
    {
        return code switch
        {
            "Assigned" => Assigned,
            "InProgress" => InProgress,
            "Completed" => Completed,
            "Rejected" => Rejected,
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