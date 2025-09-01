using Shared.Exceptions;

namespace Shared.Enums;

public enum CollateralType
{
    Land,
    Building,
    LandAndBuilding,
    Condo,
    Machine,
    Vehicle,
    Vessel
}

public static class CollateralTypeExtensions
{
    public static string ToAbbreviation(this CollateralType collateralType)
    {
        return collateralType switch
        {
            CollateralType.Land => "L",
            CollateralType.Building => "B",
            CollateralType.LandAndBuilding => "LB",
            CollateralType.Condo => "C",
            CollateralType.Machine => "M",
            CollateralType.Vehicle => "VH",
            CollateralType.Vessel => "VS",
            _ => throw new DomainException("Collateral type not recognized."),
        };
    }
    public static CollateralType FromAbbreviation(this string abbreviation)
    {
        return abbreviation switch
        {
            "L" => CollateralType.Land,
            "B" => CollateralType.Building,
            "LB" => CollateralType.LandAndBuilding,
            "C" => CollateralType.Condo,
            "M" => CollateralType.Machine,
            "VH" => CollateralType.Vehicle,
            "VS" => CollateralType.Vessel,
            _ => throw new DomainException("Collateral type not recognized."),
        };
    }
}