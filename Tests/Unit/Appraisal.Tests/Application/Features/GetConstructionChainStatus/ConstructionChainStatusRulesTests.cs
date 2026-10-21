using Appraisal.Application.Features.Appraisals.GetConstructionChainStatus;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Application.Features.GetConstructionChainStatus;

/// <summary>
/// The decision rules behind construction-chain-status, over the whole appraisal tree. The driving case is the
/// sibling one: CI#3 is picked, and the tree already holds CI#4 (completed 10/08) and a re-appraisal (completed
/// 15/08) side by side — walking back up from the pick would report CI#3 and miss both.
/// </summary>
public class ConstructionChainStatusRulesTests
{
    private static readonly DateTime D = new(2026, 8, 1);

    private static ChainAppraisal Ci(string number, int inspection, DateTime completedAt) =>
        new(Guid.NewGuid(), number, AppraisalTypes.Progressive, "06", inspection, completedAt);

    private static ChainAppraisal Re(string number, DateTime completedAt) =>
        new(Guid.NewGuid(), number, AppraisalTypes.ReAppraisal, "03", null, completedAt);

    private static ChainAppraisal Original(string number, DateTime completedAt) =>
        new(Guid.NewGuid(), number, AppraisalTypes.New, "01", null, completedAt);

    private static ChainProgress Under(decimal pct) => new(pct, true);

    private static readonly ChainProgress Done = new(100m, false);

    private static readonly OpenInspectionRequestRef OpenRequest = new(Guid.NewGuid(), "REQ-000123-2569");

    /// <summary>Completed appraisals handed over newest completion first, as the handler loads them.</summary>
    private static ConstructionChainStatusResult Evaluate(
        Guid picked,
        IEnumerable<ChainAppraisal> completed,
        Dictionary<Guid, ChainProgress>? progress = null,
        OpenInspectionRequestRef? open = null) =>
        ConstructionChainStatusRules.Evaluate(
            picked,
            completed.OrderByDescending(c => c.CompletedAt).ToList(),
            progress ?? [],
            open);

    // ── construction progress ───────────────────────────────────────────────────

    [Fact]
    public void A_tree_with_no_inspection_anywhere_is_NoData_and_not_blocked()
    {
        var original = Original("A-0", D);

        var result = Evaluate(original.Id, [original]);

        Assert.Equal(ConstructionChainStatuses.NoData, result.Construction.Status);
        Assert.Null(result.Construction.ProgressPct);
        Assert.Null(result.Construction.SourceAppraisalId);
        Assert.Null(result.BlockReason);
        Assert.Null(result.LatestAppraisal);
    }

    [Fact]
    public void Progress_comes_from_the_most_recently_completed_appraisal_that_has_inspections()
    {
        var ci1 = Ci("A-1", 1, D);
        var ci2 = Ci("A-2", 2, D.AddDays(30));

        var result = Evaluate(ci2.Id, [ci1, ci2],
            new() { [ci1.Id] = Under(30m), [ci2.Id] = Under(72.69m) });

        Assert.Equal(ConstructionChainStatuses.InProgress, result.Construction.Status);
        Assert.Equal(72.69m, result.Construction.ProgressPct);
        Assert.Equal(ci2.Id, result.Construction.SourceAppraisalId);
        Assert.Equal("A-2", result.Construction.SourceAppraisalNumber);
        Assert.Equal(2, result.Construction.SourceInspectionNumber);
        Assert.Equal(ci2.CompletedAt, result.Construction.SourceCompletedAt);
        Assert.Null(result.BlockReason);
    }

    [Fact]
    public void An_appraisal_without_inspection_data_is_skipped_when_finding_the_source()
    {
        var ci1 = Ci("A-1", 1, D);
        var reappraisal = Re("A-R", D.AddDays(20)); // newer, but the appraiser recorded no construction on it

        var result = Evaluate(ci1.Id, [ci1, reappraisal], new() { [ci1.Id] = Under(40m) });

        Assert.Equal(ci1.Id, result.Construction.SourceAppraisalId);
        Assert.Equal(40m, result.Construction.ProgressPct);
    }

    [Fact]
    public void A_reappraisal_that_did_record_construction_is_the_source_when_it_is_the_newest()
    {
        var ci1 = Ci("A-1", 1, D);
        var reappraisal = Re("A-R", D.AddDays(20));

        var result = Evaluate(ci1.Id, [ci1, reappraisal],
            new() { [ci1.Id] = Under(40m), [reappraisal.Id] = Under(55m) });

        Assert.Equal(reappraisal.Id, result.Construction.SourceAppraisalId);
        Assert.Equal(55m, result.Construction.ProgressPct);
        Assert.Null(result.Construction.SourceInspectionNumber); // not an inspection round
    }

    [Fact]
    public void Complete_progress_blocks_with_ConstructionComplete()
    {
        var ci1 = Ci("A-1", 1, D);

        var result = Evaluate(ci1.Id, [ci1], new() { [ci1.Id] = Done });

        Assert.Equal(ConstructionChainStatuses.Complete, result.Construction.Status);
        Assert.Equal(100m, result.Construction.ProgressPct);
        Assert.Equal(ConstructionChainBlockReasons.ConstructionComplete, result.BlockReason);
    }

    [Fact]
    public void Progress_that_rounds_to_100_but_is_not_finished_does_not_block()
    {
        var ci1 = Ci("A-1", 1, D);

        // 99.996% displays as 100.00 yet is still under construction; the view's flag decides, not the display.
        var result = Evaluate(ci1.Id, [ci1], new() { [ci1.Id] = new ChainProgress(100m, true) });

        Assert.Equal(ConstructionChainStatuses.InProgress, result.Construction.Status);
        Assert.Null(result.BlockReason);
    }

    // ── later inspection (sibling branches count) ───────────────────────────────

    [Fact]
    public void The_sibling_case_a_later_inspection_beside_a_reappraisal_blocks_the_older_pick()
    {
        var ci3 = Ci("A-3", 3, new DateTime(2026, 7, 1));
        var ci4 = Ci("A-4", 4, new DateTime(2026, 8, 10));
        var reappraisal = Re("A-R", new DateTime(2026, 8, 15));

        var result = Evaluate(ci3.Id, [ci3, ci4, reappraisal],
            new() { [ci3.Id] = Under(60m), [ci4.Id] = Under(80m) });

        Assert.Equal(ConstructionChainBlockReasons.LaterInspectionExists, result.BlockReason);
        Assert.Equal(ci4.Id, result.LaterInspection!.AppraisalId);
        Assert.Equal("A-4", result.LaterInspection.AppraisalNumber);
        Assert.Equal(4, result.LaterInspection.InspectionNumber);
        Assert.Equal(ci4.CompletedAt, result.LaterInspection.CompletedAt);
        // CI#4 is the newest appraisal that holds construction data: the re-appraisal carries none.
        Assert.Equal(ci4.Id, result.Construction.SourceAppraisalId);
        // Blocked already, so no "use the latest book" offer.
        Assert.Null(result.LatestAppraisal);
    }

    [Fact]
    public void Picking_the_last_inspection_leaves_the_reappraisal_as_an_offer_not_a_block()
    {
        var ci3 = Ci("A-3", 3, new DateTime(2026, 7, 1));
        var ci4 = Ci("A-4", 4, new DateTime(2026, 8, 10));
        var reappraisal = Re("A-R", new DateTime(2026, 8, 15));

        var result = Evaluate(ci4.Id, [ci3, ci4, reappraisal],
            new() { [ci3.Id] = Under(60m), [ci4.Id] = Under(80m) });

        Assert.Null(result.BlockReason);
        Assert.Null(result.LaterInspection);
        var latest = result.LatestAppraisal!;
        Assert.Equal(reappraisal.Id, latest.AppraisalId);
        Assert.Equal("A-R", latest.AppraisalNumber);
        Assert.Equal("03", latest.PurposeCode);
        Assert.Equal(reappraisal.CompletedAt, latest.CompletedAt);
    }

    [Fact]
    public void Nothing_is_offered_when_the_pick_is_already_the_latest_appraisal()
    {
        var ci1 = Ci("A-1", 1, D);

        var result = Evaluate(ci1.Id, [ci1], new() { [ci1.Id] = Under(10m) });

        Assert.Null(result.LatestAppraisal);
        Assert.Null(result.LaterInspection);
    }

    [Fact]
    public void A_later_reappraisal_alone_is_not_a_later_inspection()
    {
        var ci1 = Ci("A-1", 1, D);
        var reappraisal = Re("A-R", D.AddDays(5));

        var result = Evaluate(ci1.Id, [ci1, reappraisal], new() { [ci1.Id] = Under(10m) });

        Assert.Null(result.LaterInspection);
        Assert.Null(result.BlockReason);
        Assert.Equal(reappraisal.Id, result.LatestAppraisal!.AppraisalId);
    }

    [Fact]
    public void An_earlier_inspection_is_not_a_later_one()
    {
        var ci1 = Ci("A-1", 1, D);
        var ci2 = Ci("A-2", 2, D.AddDays(30));

        var result = Evaluate(ci2.Id, [ci1, ci2], new() { [ci2.Id] = Under(10m) });

        Assert.Null(result.LaterInspection);
    }

    [Fact]
    public void When_several_inspections_follow_the_pick_the_latest_is_reported()
    {
        var ci1 = Ci("A-1", 1, D);
        var ci2 = Ci("A-2", 2, D.AddDays(30));
        var ci3 = Ci("A-3", 3, D.AddDays(60));

        var result = Evaluate(ci1.Id, [ci1, ci2, ci3]);

        Assert.Equal(ci3.Id, result.LaterInspection!.AppraisalId);
    }

    // ── open request ────────────────────────────────────────────────────────────

    [Fact]
    public void An_open_inspection_request_blocks()
    {
        var ci1 = Ci("A-1", 1, D);

        var result = Evaluate(ci1.Id, [ci1], new() { [ci1.Id] = Under(10m) }, OpenRequest);

        Assert.Equal(ConstructionChainBlockReasons.OpenInspectionRequest, result.BlockReason);
        Assert.Equal(OpenRequest, result.OpenInspectionRequest);
    }

    // ── precedence ──────────────────────────────────────────────────────────────

    [Fact]
    public void ConstructionComplete_outranks_a_later_inspection_and_an_open_request()
    {
        var ci1 = Ci("A-1", 1, D);
        var ci2 = Ci("A-2", 2, D.AddDays(30));

        var result = Evaluate(ci1.Id, [ci1, ci2], new() { [ci2.Id] = Done }, OpenRequest);

        Assert.Equal(ConstructionChainBlockReasons.ConstructionComplete, result.BlockReason);
        Assert.NotNull(result.LaterInspection);
        Assert.NotNull(result.OpenInspectionRequest);
    }

    [Fact]
    public void A_later_inspection_outranks_an_open_request()
    {
        var ci1 = Ci("A-1", 1, D);
        var ci2 = Ci("A-2", 2, D.AddDays(30));

        var result = Evaluate(ci1.Id, [ci1, ci2], new() { [ci2.Id] = Under(20m) }, OpenRequest);

        Assert.Equal(ConstructionChainBlockReasons.LaterInspectionExists, result.BlockReason);
    }

    [Fact]
    public void A_pick_that_is_not_among_the_completed_appraisals_has_no_later_inspection()
    {
        var ci2 = Ci("A-2", 2, D);

        var result = Evaluate(Guid.NewGuid(), [ci2]);

        Assert.Null(result.LaterInspection);
    }
}
