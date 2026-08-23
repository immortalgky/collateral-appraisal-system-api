using Appraisal.Domain.Appraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Locks in the two rules that make the construction-inspection remark reliable.
///
/// 1. The remark is mode-independent. It is captured in Summary and Full Detail alike, and a mode
///    switch on its own must not wipe it — that clear is what kept the หมายเหตุ row off the
///    construction summary report for every full-detail inspection.
/// 2. Neither remark nor summary detail may exceed the storage width. Five of the twelve commands
///    carrying ConstructionInspectionData have no request validator, so the entity is the only
///    chokepoint; without the guard an over-long value reaches SaveChanges and fails as a 500.
/// </summary>
public class ConstructionInspectionRemarkTests
{
    private static ConstructionInspection SummaryWithRemark(string? remark) =>
        ConstructionInspection.CreateSummary(
            appraisalPropertyId: Guid.NewGuid(),
            totalValue: 1_000_000m,
            summaryDetail: "งานโครงสร้าง",
            summaryPreviousProgressPct: 10m,
            summaryPreviousValue: 100_000m,
            summaryCurrentProgressPct: 40m,
            summaryCurrentValue: 400_000m,
            remark: remark);

    // ── mode independence ───────────────────────────────────────────────────────

    [Fact]
    public void CreateFullDetail_keeps_the_remark()
    {
        var ci = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000_000m, "ตรวจงวดที่ 2");

        Assert.True(ci.IsFullDetail);
        Assert.Equal("ตรวจงวดที่ 2", ci.Remark);
    }

    [Fact]
    public void Switching_from_summary_to_full_detail_keeps_the_remark_and_clears_summary_state()
    {
        var ci = SummaryWithRemark("ยังเหลืองานระบบไฟฟ้า");

        ci.UpdateFullDetail(totalValue: 1_000_000m, remark: "ยังเหลืองานระบบไฟฟ้า");

        Assert.True(ci.IsFullDetail);
        Assert.Equal("ยังเหลืองานระบบไฟฟ้า", ci.Remark);
        // Summary-only state is still cleared by the switch — only the remark survives it.
        Assert.Null(ci.SummaryDetail);
        Assert.Null(ci.SummaryCurrentProgressPct);
        Assert.Null(ci.SummaryCurrentValue);
    }

    [Fact]
    public void Switching_back_to_summary_keeps_the_remark()
    {
        var ci = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000_000m, "หมายเหตุรอบนี้");

        ci.UpdateSummary(
            totalValue: 1_000_000m,
            summaryDetail: null,
            summaryPreviousProgressPct: null,
            summaryPreviousValue: null,
            summaryCurrentProgressPct: null,
            summaryCurrentValue: null,
            remark: "หมายเหตุรอบนี้");

        Assert.False(ci.IsFullDetail);
        Assert.Equal("หมายเหตุรอบนี้", ci.Remark);
    }

    [Fact]
    public void A_new_inspection_round_starts_without_the_previous_rounds_remark()
    {
        var prior = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000_000m, "รอบก่อน");
        prior.AddWorkDetail(
            constructionWorkGroupId: Guid.NewGuid(),
            workItemName: "เสาเข็ม",
            displayOrder: 1,
            proportionPct: 100m,
            previousProgressPct: 0m,
            currentProgressPct: 40m);

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid());

        Assert.Null(next.Remark);
        // The work items themselves do carry over, with last round's current becoming previous.
        var carried = Assert.Single(next.WorkDetails);
        Assert.Equal(40m, carried.PreviousProgressPct);
    }

    // ── length guard ────────────────────────────────────────────────────────────

    [Fact]
    public void A_remark_at_the_limit_is_accepted()
    {
        var atLimit = new string('ก', ConstructionInspection.RemarkMaxLength);

        var ci = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000_000m, atLimit);

        Assert.Equal(atLimit, ci.Remark);
    }

    [Fact]
    public void An_over_long_remark_is_rejected_as_bad_input()
    {
        var tooLong = new string('ก', ConstructionInspection.RemarkMaxLength + 1);

        // DomainException maps to HTTP 400, so the caller is told the input is too long
        // instead of hitting a SQL truncation error at SaveChanges.
        Assert.Throws<DomainException>(
            () => ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000_000m, tooLong));

        var existing = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000_000m, null);
        Assert.Throws<DomainException>(() => existing.UpdateFullDetail(1_000_000m, tooLong));

        Assert.Throws<DomainException>(() => SummaryWithRemark(tooLong));
    }

    [Fact]
    public void An_over_long_summary_detail_is_rejected_as_bad_input()
    {
        var tooLong = new string('ก', ConstructionInspection.SummaryDetailMaxLength + 1);

        Assert.Throws<DomainException>(() => ConstructionInspection.CreateSummary(
            appraisalPropertyId: Guid.NewGuid(),
            totalValue: 1_000_000m,
            summaryDetail: tooLong,
            summaryPreviousProgressPct: null,
            summaryPreviousValue: null,
            summaryCurrentProgressPct: null,
            summaryCurrentValue: null,
            remark: null));
    }
}
