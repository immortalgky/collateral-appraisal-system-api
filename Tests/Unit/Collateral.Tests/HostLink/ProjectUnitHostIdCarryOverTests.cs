using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;

namespace Collateral.Tests.HostLink;

/// <summary>
/// Covers <see cref="CollateralMasterUpsertService.CarryHostCollateralIds"/>.
///
/// Every appraisal of a block project rebuilds the unit set wholesale
/// (<c>ProjectDetail.ReplaceUnits</c>), and the appraisal snapshot carries no AS400 collateral id.
/// Without this carry-over a reappraisal would erase the ids AS400 issued for the project's financed
/// units, so these tests pin both the match and the refusal to guess.
/// </summary>
public class ProjectUnitHostIdCarryOverTests
{
    private static readonly Guid MasterId = Guid.CreateVersion7();

    private static ProjectUnit Condo(int sequence, string roomNumber, string? hostId = null)
    {
        var unit = ProjectUnit.CreateCondo(MasterId, sequence, roomNumber: roomNumber);
        unit.SetHostCollateralId(hostId);
        return unit;
    }

    private static ProjectUnit LandAndBuilding(int sequence, string plotNumber, string? hostId = null)
    {
        var unit = ProjectUnit.CreateLandAndBuilding(MasterId, sequence, plotNumber: plotNumber);
        unit.SetHostCollateralId(hostId);
        return unit;
    }

    [Fact]
    public void CarriesTheIdWhenSequenceAndRoomNumberBothMatch()
    {
        var existing = new[] { Condo(1, "A-501", "25909") };
        var incoming = new[] { Condo(1, "A-501") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(1, result.Carried);
        Assert.Empty(result.Dropped);
        Assert.Equal("25909", incoming[0].HostCollateralId);
    }

    [Fact]
    public void CarriesTheIdForLandAndBuildingUnitsByPlotNumber()
    {
        var existing = new[] { LandAndBuilding(3, "P-003", "25910") };
        var incoming = new[] { LandAndBuilding(3, "P-003") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(1, result.Carried);
        Assert.Equal("25910", incoming[0].HostCollateralId);
    }

    [Fact]
    public void DropsTheIdWhenTheIdentityFieldDisagrees()
    {
        // Same slot in the list, different room: a different unit now sits at sequence 1.
        var existing = new[] { Condo(1, "A-501", "25909") };
        var incoming = new[] { Condo(1, "B-101") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(0, result.Carried);
        Assert.Null(incoming[0].HostCollateralId);
        Assert.Equal("25909", Assert.Single(result.Dropped).HostCollateralId);
    }

    [Fact]
    public void DoesNotFollowAUnitThatMovedToAnotherSequenceNumber()
    {
        var existing = new[] { Condo(2, "A-502", "25909") };
        var incoming = new[] { Condo(5, "A-502") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(0, result.Carried);
        Assert.Null(incoming[0].HostCollateralId);
        Assert.Single(result.Dropped);
    }

    [Fact]
    public void CarriesOnlyTheUnitsThatActuallyHoldAnId()
    {
        var existing = new[] { Condo(1, "A-501", "25909"), Condo(2, "A-502"), Condo(3, "A-503", "25911") };
        var incoming = new[] { Condo(1, "A-501"), Condo(2, "A-502"), Condo(3, "A-503") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(2, result.Carried);
        Assert.Empty(result.Dropped);
        Assert.Equal("25909", incoming[0].HostCollateralId);
        Assert.Null(incoming[1].HostCollateralId);
        Assert.Equal("25911", incoming[2].HostCollateralId);
    }

    [Fact]
    public void ReportsUnitsThatLeftTheProjectInsteadOfReassigningTheirIds()
    {
        // Unit 2 was sold and removed from the new upload; its id must not migrate to unit 3.
        var existing = new[] { Condo(2, "A-502", "25909") };
        var incoming = new[] { Condo(1, "A-501"), Condo(3, "A-503") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(0, result.Carried);
        Assert.All(incoming, u => Assert.Null(u.HostCollateralId));
        Assert.Equal("25909", Assert.Single(result.Dropped).HostCollateralId);
    }

    [Fact]
    public void FirstAppraisalOfAProjectHasNothingToCarry()
    {
        var incoming = new[] { Condo(1, "A-501") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(null, incoming);

        Assert.Equal(0, result.Carried);
        Assert.Empty(result.Dropped);
        Assert.Null(incoming[0].HostCollateralId);
    }

    [Fact]
    public void TreatsIdentityFieldsAsEqualIgnoringSurroundingWhitespaceAndCase()
    {
        var existing = new[] { Condo(1, " a-501 ", "25909") };
        var incoming = new[] { Condo(1, "A-501") };

        var result = CollateralMasterUpsertService.CarryHostCollateralIds(existing, incoming);

        Assert.Equal(1, result.Carried);
        Assert.Equal("25909", incoming[0].HostCollateralId);
    }
}
