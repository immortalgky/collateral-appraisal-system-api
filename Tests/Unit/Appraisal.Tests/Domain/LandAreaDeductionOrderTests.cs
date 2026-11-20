using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Deductions keep the order of the request's <c>landAreaDeductions</c> array through a stored
/// SequenceNumber, as titles do — their Guid v7 ids alone sort randomly in SQL Server.
/// </summary>
public class LandAreaDeductionOrderTests
{
    private static LandAppraisalDetail NewLand() =>
        Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: new DateTime(2026, 1, 1))
            .AddLandProperty().LandDetail!;

    private static LandAreaDeductionData Data(string reason, Guid? id = null) => new(id, reason, AreaInSqWa: 1m);

    [Fact]
    public void Sync_numbers_new_deductions_in_request_order()
    {
        var land = NewLand();

        LandDetailSync.SyncDeductions(land, [Data("03"), Data("01"), Data("02")]);

        Assert.Equal(["03", "01", "02"], land.Deductions.Select(d => d.ReasonCode));
        Assert.Equal([1, 2, 3], land.Deductions.Select(d => d.SequenceNumber));
    }

    [Fact]
    public void Sync_reorders_existing_rows_without_changing_their_ids()
    {
        var land = NewLand();
        LandDetailSync.SyncDeductions(land, [Data("01"), Data("02"), Data("03")]);
        foreach (var d in land.Deductions) d.Id = Guid.CreateVersion7(); // EF assigns ids on save
        var ids = land.Deductions.ToDictionary(d => d.ReasonCode, d => d.Id);

        LandDetailSync.SyncDeductions(land, [Data("03", ids["03"]), Data("01", ids["01"]), Data("04"), Data("02", ids["02"])]);

        Assert.Equal(["03", "01", "04", "02"], land.Deductions.Select(d => d.ReasonCode));
        Assert.Equal([1, 2, 3, 4], land.Deductions.Select(d => d.SequenceNumber));
        Assert.Equal(ids["01"], land.Deductions.Single(d => d.ReasonCode == "01").Id);
    }

    [Fact]
    public void An_unnumbered_deduction_goes_last()
    {
        var land = NewLand();
        foreach (var reason in new[] { "02", "01" })
            land.AddDeduction(LandAreaDeduction.Create(land.Id, reason));

        Assert.Equal(["02", "01"], land.Deductions.Select(d => d.ReasonCode));
        Assert.Equal([1, 2], land.Deductions.Select(d => d.SequenceNumber));
    }
}
