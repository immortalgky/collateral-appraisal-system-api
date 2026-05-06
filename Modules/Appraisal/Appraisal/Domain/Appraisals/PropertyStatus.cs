namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing the lifecycle status of an AppraisalProperty.
/// A property starts as Draft and is promoted to Saved once all required
/// data for that property type has been captured by the appraiser.
/// Pricing analysis cannot start until all properties in the group are Saved.
/// </summary>
public class PropertyStatus : ValueObject
{
    public string Code { get; }

    private PropertyStatus(string code)
    {
        Code = code;
    }

    public static PropertyStatus Draft => new("Draft");
    public static PropertyStatus Saved => new("Saved");

    public static PropertyStatus FromString(string code)
    {
        return code switch
        {
            "Draft" => Draft,
            "Saved" => Saved,
            _ => throw new ArgumentException($"Invalid property status: {code}")
        };
    }

    public static implicit operator string(PropertyStatus status) => status.Code;

    public static bool operator ==(PropertyStatus? left, PropertyStatus? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(PropertyStatus? left, PropertyStatus? right) => !(left == right);

    public override bool Equals(object? obj) => obj is PropertyStatus other && Code == other.Code;
    public override int GetHashCode() => Code.GetHashCode();
    public override string ToString() => Code;
}
