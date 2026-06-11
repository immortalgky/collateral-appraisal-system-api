namespace Collateral.Contracts;

/// <summary>
/// Canonical collateral type codes. Vocabulary aligned with Appraisal module's PropertyType.Code.
/// CollateralType = PropertyType.Code (identity mapping — no translation needed at the boundary).
/// Moved to Collateral.Contracts so both the Collateral implementation and Integration format
/// utilities can reference these codes without a circular dependency.
/// </summary>
public static class CollateralTypes
{
    /// <summary>Bare land (no building).</summary>
    public const string Land = "L";

    /// <summary>Land with building(s).</summary>
    public const string LandWithBuilding = "LB";

    /// <summary>Condo unit.</summary>
    public const string Condo = "U";

    /// <summary>Bare leasehold land.</summary>
    public const string Leasehold = "LSL";

    /// <summary>Building on leasehold land (no underlying land deeded to lessee).</summary>
    public const string LeaseholdBuilding = "LSB";

    /// <summary>Leasehold land + building.</summary>
    public const string LeaseholdWithBuilding = "LS";

    /// <summary>Machinery / equipment.</summary>
    public const string Machine = "MAC";

    /// <summary>Block project (condo-block or land-and-building village).</summary>
    public const string Project = "PRJ";
}
