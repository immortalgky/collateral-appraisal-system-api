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

    /// <summary>Leasehold condo unit — the underlying collateral is a condo, not land.</summary>
    public const string LeaseholdCondo = "LSU";

    /// <summary>Machinery / equipment.</summary>
    public const string Machine = "MAC";

    /// <summary>Block project (condo-block or land-and-building village).</summary>
    public const string Project = "PRJ";

    /// <summary>
    /// Collateral carried over from AS400 whose physical identity we do not have.
    ///
    /// The AS400 legacy listing gives a collateral id, a valuation date and a value, but no title
    /// number and no location — so there is nothing to build a LandDetail / CondoDetail from, and
    /// nothing to dedup on. A master of this type therefore carries NO detail row at all.
    ///
    /// It is a real code rather than a borrowed one on purpose: reusing <see cref="Land"/> would
    /// make every type-gated branch in the system treat it as a deeded parcel and report land
    /// attributes it does not have. Falling into the "unrecognised type" branch is the honest
    /// outcome. If AS400 ever supplies the identifying data, such a master can be upgraded in place.
    /// </summary>
    public const string Unidentified = "UNK";

    /// <summary>
    /// Every code in the leasehold family. Use this instead of spelling the codes out one by one —
    /// the list was duplicated across the repository, the lookup handler and the export writers, and
    /// <see cref="LeaseholdCondo"/> was missed by all of them when it was added to PropertyType.
    /// </summary>
    public static readonly string[] LeaseholdFamily =
        [Leasehold, LeaseholdBuilding, LeaseholdWithBuilding, LeaseholdCondo];
}
