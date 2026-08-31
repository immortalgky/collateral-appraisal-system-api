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
        => new(appraisalNumber, hostId, CollateralName: null, Address1: null, recordDate, indicator,
            LocationCode: null, CollateralCode: null, PropertyType: null, PropertyTypeDesc: null,
            MasterTitle: "Y", RowHash: $"{indicator}{recordDate}{hostId}");

    /// <summary>
    /// Redemption wins regardless of the dates on the rows. RecordDate is the file's transmission
    /// date, identical on every row of a real file, so it carries no information about which event
    /// happened later — see PickWinningRecord's own remarks.
    /// </summary>
    [Fact]
    public void PickWinningRecord_RedemptionWins_RegardlessOfRecordDate()
    {
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(R, new DateOnly(2025, 1, 10)),
            Record(D, new DateOnly(2026, 8, 1))
        ]);

        Assert.Equal(R, winner.RecordIndicator);
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

    /// <summary>
    /// A drawdown does NOT override a redemption in the same file, whatever the dates say.
    ///
    /// Re-pledging after a release is real, but a file cannot express it: both rows carry the same
    /// transmission date, so there is nothing to order them by. Reporting released collateral as
    /// still held would overstate the bank's exposure to the regulator, so the safe reading wins and
    /// the next file corrects it.
    /// </summary>
    [Fact]
    public void PickWinningRecord_DrawdownDoesNotOverrideRedemptionInTheSameFile()
    {
        var winner = HostCollateralLinkIngestor.PickWinningRecord([
            Record(R, new DateOnly(2025, 3, 1)),
            Record(D, new DateOnly(2026, 7, 1), hostId: "999")
        ]);

        Assert.Equal(R, winner.RecordIndicator);
    }
}
