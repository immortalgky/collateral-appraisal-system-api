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
    public static PropertyType Land => new("L");
    public static PropertyType Building => new("B");
    public static PropertyType LandAndBuilding => new("LB");
    public static PropertyType Condo => new("U");
    public static PropertyType Vehicle => new("VEH");
    public static PropertyType Vessel => new("VES");
    public static PropertyType Machinery => new("MAC");
    public static PropertyType LeaseAgreementLand => new("LSL");
    public static PropertyType LeaseAgreementBuilding => new("LSB");
    public static PropertyType LeaseAgreementLandAndBuilding => new("LS");

    // Convenience helpers
    public bool IsLeaseAgreement => Code is "LSL" or "LSB" or "LS";
    public bool HasLandDetail => Code is "L" or "LB" or "LSL" or "LS";
    public bool HasBuildingDetail => Code is "B" or "LB" or "LSB" or "LS";

    // Factory method from string
    public static PropertyType FromString(string code)
    {
        return code switch
        {
            "L" => Land,
            "B" => Building,
            "LB" => LandAndBuilding,
            "U" => Condo,
            "VEH" => Vehicle,
            "VES" => Vessel,
            "MAC" => Machinery,
            "LSL" => LeaseAgreementLand,
            "LSB" => LeaseAgreementBuilding,
            "LS" => LeaseAgreementLandAndBuilding,
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
        Machinery,
        LeaseAgreementLand,
        LeaseAgreementBuilding,
        LeaseAgreementLandAndBuilding
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