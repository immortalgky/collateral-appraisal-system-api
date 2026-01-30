namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value object representing the type of property being appraised.
/// Determines which property detail table is used (1:1 relationship).
/// </summary>
public class PropertyType : ValueObject
{
    public string Code { get; }

    private PropertyType(string code)
    {
        Code = code;
    }

    // Predefined types
    public static PropertyType Land => new("Land");
    public static PropertyType Building => new("Building");
    public static PropertyType LandAndBuilding => new("LandAndBuilding");
    public static PropertyType Condo => new("Condo");
    public static PropertyType Vehicle => new("Vehicle");
    public static PropertyType Vessel => new("Vessel");
    public static PropertyType Machinery => new("Machinery");

    // Factory method from string
    public static PropertyType FromString(string code)
    {
        return code switch
        {
            "Land" => Land,
            "Building" => Building,
            "LandAndBuilding" => LandAndBuilding,
            "Condo" => Condo,
            "Vehicle" => Vehicle,
            "Vessel" => Vessel,
            "Machinery" => Machinery,
            _ => throw new ArgumentException($"Invalid property type: {code}")
        };
    }

    /// <summary>
    /// Get all valid property types
    /// </summary>
    public static IReadOnlyList<PropertyType> All =>
    [
        Land,
        Building,
        LandAndBuilding,
        Condo,
        Vehicle,
        Vessel,
        Machinery
    ];

    // Implicit conversion to string
    public static implicit operator string(PropertyType type)
    {
        return type.Code;
    }

    // Equality
    public static bool operator ==(PropertyType? left, PropertyType? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Code == right.Code;
    }

    public static bool operator !=(PropertyType? left, PropertyType? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is PropertyType other)
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