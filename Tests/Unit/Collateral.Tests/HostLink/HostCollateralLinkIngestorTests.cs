using Collateral.CollateralMasters.HostLink;
using Integration.Contracts.HostLink;

namespace Collateral.Tests.HostLink;

/// <summary>
/// Pins the winner-selection rule for when one COLLATLINK file carries several rows for the same
/// appraisal number.
///
/// This matters because AS400 orders rows in the file by collateral id, not by date. Deciding on
/// file position could discard a redemption merely because it happens to sit before an older
/// drawdown, after which released collateral would keep being reported as still held.
/// </summary>
public class HostCollateralLinkIngestorTests
{
    private const string D = HostLinkRecordIndicators.Drawdown;
    private const string R = HostLinkRecordIndicators.Redeemed;

    private static ParsedHostLinkRecord Record(
        string indicator,
        DateOnly? recordDate,
        string hostId = "111",
        string appraisalNumber = "69000001")
        => new(appraisalNumber, hostId, recordDate, indicator, RowHash: $"{indicator}{recordDate}{hostId}");

    /// <summary>The newest RecordDate wins, not the file order.</summary>
    [Fact]
    public void PickWinningRecord_PrefersNewestRecordDate_NotFileOrder()
    {
        // The redemption (newer) sits before the drawdown (older) — taking .Last() by file order
        // would pick the wrong one.
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(R, new DateOnly(2026, 8, 1)),
            Record(D, new DateOnly(2025, 1, 10))
        ]);

        Assert.Equal(R, winner.RecordIndicator);
        Assert.Equal(new DateOnly(2026, 8, 1), winner.RecordDate);
    }

    /// <summary>On an equal date, redemption beats drawdown as the later lifecycle state.</summary>
    [Fact]
    public void PickWinningRecord_SameDate_RedemptionBeatsDrawdown()
    {
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(R, new DateOnly(2026, 8, 1)),
            Record(D, new DateOnly(2026, 8, 1))
        ]);

        Assert.Equal(R, winner.RecordIndicator);
    }

    /// <summary>
    /// An undated redemption must NOT be lost, even when a dated drawdown is in the same file.
    ///
    /// AS400 can send a blank, "00000000" or out-of-range RecordDate, for which
    /// ParseDdmmyyyyOrNull returns null. If nulls sank to the bottom of the ordering, an 'R' would
    /// lose to an older 'D' and released collateral would keep being reported to the regulator as
    /// still held — so the tie is biased towards the safe direction.
    /// </summary>
    [Fact]
    public void PickWinningRecord_UndatedRedemption_StillWins()
    {
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(D, new DateOnly(2026, 8, 1)),
            Record(R, null)
        ]);

        Assert.Equal(R, winner.RecordIndicator);
    }

    /// <summary>
    /// Conversely, an undated drawdown must not mask a clearly dated redemption — released
    /// collateral must not be resurrected as pledged.
    /// </summary>
    [Fact]
    public void PickWinningRecord_UndatedDrawdown_DoesNotOverrideDatedRedemption()
    {
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(D, null),
            Record(R, new DateOnly(2026, 8, 1))
        ]);

        Assert.Equal(R, winner.RecordIndicator);
    }

    /// <summary>A single row is returned as-is.</summary>
    [Fact]
    public void PickWinningRecord_SingleRecord_ReturnsIt()
    {
        var only = Record(D, new DateOnly(2026, 8, 1));

        Assert.Same(only, HostCollateralLinkIngestor.PickWinningRecord([only]));
    }

    /// <summary>The result must not depend on input order.</summary>
    [Fact]
    public void PickWinningRecord_IsIndependentOfInputOrder()
    {
        var redeemed = Record(R, new DateOnly(2026, 8, 1), hostId: "222");
        var drawdown = Record(D, new DateOnly(2025, 1, 10), hostId: "111");

        var forward = HostCollateralLinkIngestor.PickWinningRecord([redeemed, drawdown]);
        var reversed = HostCollateralLinkIngestor.PickWinningRecord([drawdown, redeemed]);

        Assert.Equal(forward.HostCollateralId, reversed.HostCollateralId);
        Assert.Equal(forward.RecordIndicator, reversed.RecordIndicator);
    }

    /// <summary>Re-pledged after an earlier redemption — the current state must be 'D'.</summary>
    [Fact]
    public void PickWinningRecord_NewerDrawdownBeatsOlderRedemption()
    {
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(R, new DateOnly(2025, 3, 1)),
            Record(D, new DateOnly(2026, 7, 1), hostId: "999")
        ]);

        Assert.Equal(D, winner.RecordIndicator);
        Assert.Equal("999", winner.HostCollateralId);
    }
}
