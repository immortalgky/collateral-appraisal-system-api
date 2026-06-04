using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Verifies that <see cref="Appraisal.Domain.Appraisals.Appraisal.CopyProperty"/> clones the
/// Lease Agreement + Rental Info for a rented-out plain Land / Land &amp; Building property
/// (flag-gated), and leaves a non-rented land copy without lease/rental rows.
///
/// Locks in the eager-load + deep-copy contract behind the "Land rented out to others" feature:
/// if a future change drops a child-collection Include in GetByIdWithPropertiesAsync or a CopyFrom
/// stops deep-copying, the count assertions here fail.
/// </summary>
public class CopyRentedOutLandTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    // NOTE: these tests add exactly one property before copying. AppraisalProperty.Create leaves
    // Id == Guid.Empty in unit tests (no EF), so CopyProperty(source.Id) resolves the single source
    // via FirstOrDefault. Do NOT add a second property before the copy — both would share
    // Guid.Empty and the wrong source would be matched silently.
    private static Appraisal.Domain.Appraisals.Appraisal CreateAppraisal() =>
        Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(),
            appraisalType: "New",
            priority: "Normal",
            now: new DateTime(2026, 1, 1));

    /// <summary>Populate a property's land detail flag + lease + rental (with all 4 child collections).</summary>
    private static void MakeRentedOut(AppraisalProperty property)
    {
        property.LandDetail!.Update(propertyName: "Rented Plot", isRentedOut: true);

        var lease = LeaseAgreementDetail.Create(property.Id);
        lease.Update(lesseeName: "Acme Co", lessorName: "Owner A", contractNo: "C-001");
        property.SetLeaseAgreementDetail(lease);

        var rental = RentalInfo.Create(property.Id);
        rental.Update(numberOfYears: 3, contractRentalFeePerYear: 12000m, growthRateType: "Period");
        rental.AddUpFrontEntry(new DateTime(2026, 1, 1), 1000m);
        rental.AddGrowthPeriodEntry(fromYear: 1, toYear: 3, growthRate: 5m, growthAmount: 600m, totalAmount: 12600m);
        rental.AddScheduleEntry(year: 1, contractStart: new DateTime(2026, 1, 1), contractEnd: new DateTime(2026, 12, 31),
            upFront: 1000m, contractRentalFee: 12000m, totalAmount: 13000m, growthRatePercent: 5m);
        rental.SetScheduleOverride(year: 2, upFront: 1100m, contractRentalFee: 12500m);
        property.SetRentalInfo(rental);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void CopyProperty_RentedOutLand_CopiesLeaseAndRentalWithChildren()
    {
        var appraisal = CreateAppraisal();
        var source = appraisal.AddLandProperty();
        MakeRentedOut(source);

        var copy = appraisal.CopyProperty(source.Id);

        // Flag carried forward
        Assert.True(copy.LandDetail!.IsRentedOut);

        // Lease agreement deep-copied (scalars), as a fresh instance
        Assert.NotNull(copy.LeaseAgreementDetail);
        Assert.NotSame(source.LeaseAgreementDetail, copy.LeaseAgreementDetail);
        Assert.Equal("Acme Co", copy.LeaseAgreementDetail!.LesseeName);
        Assert.Equal("C-001", copy.LeaseAgreementDetail.ContractNo);

        // Rental info + all four child collections deep-copied
        Assert.NotNull(copy.RentalInfo);
        Assert.NotSame(source.RentalInfo, copy.RentalInfo);
        Assert.Equal(3, copy.RentalInfo!.NumberOfYears);
        Assert.Single(copy.RentalInfo.UpFrontEntries);
        Assert.Single(copy.RentalInfo.GrowthPeriodEntries);
        Assert.Single(copy.RentalInfo.ScheduleEntries);
        Assert.Single(copy.RentalInfo.ScheduleOverrides);

        // Child entries are fresh instances, not shared references to the source graph
        Assert.NotSame(source.RentalInfo!.ScheduleEntries[0], copy.RentalInfo.ScheduleEntries[0]);

        // Source graph is read, not mutated, by the copy
        Assert.Single(source.RentalInfo.ScheduleEntries);
        Assert.Single(source.RentalInfo.ScheduleOverrides);
    }

    [Fact]
    public void CopyProperty_NonRentedLand_DoesNotCopyLeaseOrRental()
    {
        var appraisal = CreateAppraisal();
        var source = appraisal.AddLandProperty();

        // Give the source full lease/rental data, then flip the flag OFF — so the IsRentedOut
        // guard in CopyRentedOutLeaseInfo is the ONLY thing that can stop the copy. (If the guard
        // were removed, lease/rental would be copied and these assertions would fail.)
        MakeRentedOut(source);
        source.LandDetail!.Update(isRentedOut: false);

        var copy = appraisal.CopyProperty(source.Id);

        Assert.NotNull(copy.LandDetail);
        Assert.NotEqual(true, copy.LandDetail!.IsRentedOut);
        Assert.Null(copy.LeaseAgreementDetail);
        Assert.Null(copy.RentalInfo);
    }

    [Fact]
    public void CopyProperty_RentedOutLandAndBuilding_CopiesLeaseAndRental()
    {
        var appraisal = CreateAppraisal();
        var source = appraisal.AddLandAndBuildingProperty();
        MakeRentedOut(source);

        var copy = appraisal.CopyProperty(source.Id);

        Assert.True(copy.LandDetail!.IsRentedOut);
        Assert.NotNull(copy.BuildingDetail);
        Assert.NotNull(copy.LeaseAgreementDetail);
        Assert.NotNull(copy.RentalInfo);
        Assert.Single(copy.RentalInfo!.ScheduleEntries);
        Assert.Single(copy.RentalInfo.ScheduleOverrides);
    }
}
