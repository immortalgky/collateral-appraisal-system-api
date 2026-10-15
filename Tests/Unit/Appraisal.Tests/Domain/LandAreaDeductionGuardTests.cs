using Appraisal.Domain.Appraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Covers the deed guard in <see cref="LandAppraisalDetail.RecalculateDeductedArea"/>: deductions may
/// not add up to more than the registered title area, but only the finished list is checked.
/// </summary>
public class LandAreaDeductionGuardTests
{
    private static LandAppraisalDetail LandWithDeed(decimal? squareWa)
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: new DateTime(2026, 1, 1));
        var land = appraisal.AddLandProperty().LandDetail!;

        var title = LandTitle.Create(land.Id, "1218", "โฉนดที่ดิน");
        title.Update(null, null, null, null, null, null, null, null,
            squareWa is null ? null : LandArea.Create(0, 0, squareWa),
            null, null, null, null, null, null, null);
        land.AddTitle(title);
        return land;
    }

    private static void AddTitleWithoutArea(LandAppraisalDetail land) =>
        land.AddTitle(LandTitle.Create(land.Id, "1219", "โฉนดที่ดิน"));

    private static void Deduct(LandAppraisalDetail land, decimal squareWa)
    {
        var deduction = LandAreaDeduction.Create(land.Id, "05");
        deduction.Update(null, squareWa, null);
        land.AddDeduction(deduction);
    }

    [Fact]
    public void Deductions_over_the_deed_are_refused()
    {
        var land = LandWithDeed(100);
        Deduct(land, 60);
        Deduct(land, 50);

        Assert.Throws<DomainException>(land.RecalculateDeductedArea);
    }

    [Fact]
    public void Deductions_up_to_the_deed_are_accepted()
    {
        var land = LandWithDeed(100);
        Deduct(land, 60);
        Deduct(land, 40);

        land.RecalculateDeductedArea();

        Assert.Equal(100m, land.DeductedAreaInSqWa);
        Assert.Equal(0m, land.NetLandAreaInSqWa);
    }

    [Fact]
    public void An_over_deduction_mid_sync_is_not_refused_until_the_list_is_settled()
    {
        // The update handlers add rows before editing the existing ones in place, so the running
        // total can pass the deed on the way to a valid list.
        var land = LandWithDeed(100);
        Deduct(land, 80);
        Deduct(land, 50);

        land.Deductions[0].Update(null, 30, null);
        land.RecalculateDeductedArea();

        Assert.Equal(80m, land.DeductedAreaInSqWa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]     // area inputs left at their 0-0-0 default
    public void A_deed_without_an_area_does_not_block_saving(int? squareWa)
    {
        var land = LandWithDeed(squareWa);
        Deduct(land, 50);

        land.RecalculateDeductedArea();

        Assert.Equal(50m, land.DeductedAreaInSqWa);
    }

    [Fact]
    public void A_second_title_without_an_area_does_not_block_saving()
    {
        // The registered total is only the first title's 50 so far — too partial to judge 80 by.
        var land = LandWithDeed(50);
        AddTitleWithoutArea(land);
        Deduct(land, 80);

        land.RecalculateDeductedArea();

        Assert.Equal(80m, land.DeductedAreaInSqWa);
    }

    [Fact]
    public void Deductions_are_compared_at_the_stored_two_decimal_scale()
    {
        // A 50.554 deed comes back from decimal(10,2) as 50.55 while the deduction keeps 4 places;
        // the save that stored both must not start failing on the next one.
        var land = LandWithDeed(50.55m);
        Deduct(land, 50.554m);

        land.RecalculateDeductedArea();

        Assert.Equal(50.554m, land.DeductedAreaInSqWa);
    }

    [Fact]
    public void Each_title_is_rounded_the_way_it_is_stored()
    {
        // Three 33.334 titles sum to 100.002 in memory but are stored as 33.33 each = 99.99, so a
        // 100 deduction must be refused now rather than on the next save.
        var land = LandWithDeed(33.334m);
        for (var i = 0; i < 2; i++)
        {
            var title = LandTitle.Create(land.Id, $"t{i}", "โฉนดที่ดิน");
            title.Update(null, null, null, null, null, null, null, null, LandArea.Create(0, 0, 33.334m),
                null, null, null, null, null, null, null);
            land.AddTitle(title);
        }
        Deduct(land, 100m);

        Assert.Throws<DomainException>(land.RecalculateDeductedArea);
    }
}
