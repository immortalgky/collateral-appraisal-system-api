namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing assignment type (internal vs external)
/// </summary>
public class AssignmentType : ValueObject
{
    public string Code { get; }

    private AssignmentType(string code)
    {
        Code = code;
    }

    // Predefined types
    public static AssignmentType Internal => new("INTERNAL");
    public static AssignmentType External => new("EXTERNAL");

    // Factory method from string
    public static AssignmentType FromString(string code)
    {
        return code switch
        {
            "INTERNAL" => Internal,
            "Internal" => Internal,
            "EXTERNAL" => External,
            "External" => External,
            _ => throw new ArgumentException($"Invalid assignment type: {code}")
        };
    }

    // Implicit conversion to string
    public static implicit operator string(AssignmentType type)
    {
        return type.Code;
    }

    // Equality
    public static bool operator ==(AssignmentType? left, AssignmentType? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(AssignmentType? left, AssignmentType? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is AssignmentType other)
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
