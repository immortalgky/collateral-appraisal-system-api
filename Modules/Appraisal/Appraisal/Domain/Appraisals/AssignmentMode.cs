namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing assignment mode (internal vs external)
/// </summary>
public class AssignmentMode : ValueObject
{
    public string Code { get; }

    private AssignmentMode(string code)
    {
        Code = code;
    }

    // Predefined modes
    public static AssignmentMode Internal => new("Internal");
    public static AssignmentMode External => new("External");

    // Factory method from string
    public static AssignmentMode FromString(string code)
    {
        return code switch
        {
            "Internal" => Internal,
            "External" => External,
            _ => throw new ArgumentException($"Invalid assignment mode: {code}")
        };
    }

    // Implicit conversion to string
    public static implicit operator string(AssignmentMode mode)
    {
        return mode.Code;
    }

    // Equality
    public static bool operator ==(AssignmentMode? left, AssignmentMode? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(AssignmentMode? left, AssignmentMode? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is AssignmentMode other)
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