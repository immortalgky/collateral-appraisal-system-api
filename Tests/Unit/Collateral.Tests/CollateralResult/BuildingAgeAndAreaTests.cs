using Collateral.CollateralMasters.CollateralResult;
using Collateral.Contracts;

namespace Collateral.Tests.CollateralResult;

/// <summary>
/// Pins the collateral-type dispatch behind the AS400 Collateral Result fields CCEBIL (Building Age,
/// positions 199-201) and CCEARE (Area Utilization, positions 202-208).
///
/// Two sources, chosen by type: building types read every row of CollateralEngagementBuildings —
/// SUM of area, MAX of age — while a condo reads the master's CondoDetails. Everything else reports
/// nothing. The same rule lives in <c>vw_RegulatoryExport</c>; the two must stay in step.
/// </summary>
public class BuildingAgeAndAreaTests
{
    // ── Building types: aggregate across every building on the engagement ───────────────

    [Theory]
    [InlineData(CollateralTypes.LandWithBuilding)]
    [InlineData(CollateralTypes.LeaseholdBuilding)]
    [InlineData(CollateralTypes.LeaseholdWithBuilding)]
    public void BuildingTypes_UseEngagementBuildings_NotCondoDetails(string collateralType)
    {
        Assert.Equal(18, CollateralResultQuery.ToBuildingAge(collateralType, condoBuildingAge: 3, buildingsMaxAge: 18));
        Assert.Equal(240.00m, CollateralResultQuery.ToAreaUtilization(collateralType, condoUsableArea: 55m, buildingsTotalArea: 240.00m));
    }

    [Fact]
    public void BuildingType_WithNoBuildings_ReportsNothing()
    {
        // Bare-land engagements carry zero building rows, so SUM/MAX arrive as NULL.
        Assert.Null(CollateralResultQuery.ToBuildingAge(CollateralTypes.LandWithBuilding, null, null));
        Assert.Null(CollateralResultQuery.ToAreaUtilization(CollateralTypes.LandWithBuilding, null, null));
    }

    // ── Condo: last-known state on the master ──────────────────────────────────────────

    [Fact]
    public void Condo_UsesCondoDetails_AndIgnoresEngagementBuildings()
    {
        Assert.Equal(7, CollateralResultQuery.ToBuildingAge(CollateralTypes.Condo, condoBuildingAge: 7, buildingsMaxAge: 40));
        Assert.Equal(58.75m, CollateralResultQuery.ToAreaUtilization(CollateralTypes.Condo, condoUsableArea: 58.75m, buildingsTotalArea: 900m));
    }

    // ── Types that carry neither ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(CollateralTypes.Land)]
    [InlineData(CollateralTypes.Leasehold)]
    [InlineData(CollateralTypes.Machine)]
    [InlineData(CollateralTypes.Project)]
    public void NonBuildingTypes_ReportNothing(string collateralType)
    {
        // Bare land holds its area in sq.wa, a different unit from this field's sq.m — sending it would
        // be silently wrong, so the field goes out as zeros instead.
        Assert.Null(CollateralResultQuery.ToBuildingAge(collateralType, condoBuildingAge: 9, buildingsMaxAge: 9));
        Assert.Null(CollateralResultQuery.ToAreaUtilization(collateralType, condoUsableArea: 90m, buildingsTotalArea: 90m));
    }

    // ── Field-width guards: a bad value is dropped, never truncated ────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 999)]
    [InlineData(1000, null)]
    [InlineData(-1, null)]
    public void BuildingAge_OutsideFieldRange_IsDropped(int age, int? expected)
    {
        Assert.Equal(expected, CollateralResultQuery.ToBuildingAge(CollateralTypes.Condo, age, null));
    }

    [Theory]
    [InlineData("99999.99", "99999.99")]
    [InlineData("100000.00", null)]
    [InlineData("-0.01", null)]
    [InlineData("0", "0")]
    public void AreaUtilization_OutsideFieldRange_IsDropped(string area, string? expected)
    {
        var result = CollateralResultQuery.ToAreaUtilization(
            CollateralTypes.LandWithBuilding, null, decimal.Parse(area));

        Assert.Equal(expected is null ? null : decimal.Parse(expected), result);
    }

    [Fact]
    public void AreaUtilization_TotalOverflows_EvenWhenEachBuildingFits()
    {
        // Two buildings that each fit the dec(7,2) field can still overflow once combined — the guard
        // has to sit on the total, which is why it moved out of the per-building CTE.
        Assert.Null(CollateralResultQuery.ToAreaUtilization(
            CollateralTypes.LandWithBuilding, null, buildingsTotalArea: 60000m + 60000m));
    }
}
