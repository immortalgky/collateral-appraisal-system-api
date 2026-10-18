using Request.Domain.Requests;
using Shared.Exceptions;
using Shared.Models;

namespace Request.Tests.Request.Reappraisal;

/// <summary>
/// A request Initiate created for a COLLATREV book has its purpose and prior appraisal locked from the
/// start; a manual draft does not. Resending the stored values is always fine (the form re-sends the whole
/// draft on every save).
/// </summary>
public class InitiateDraftLockTests
{
    private static readonly Guid PriorId = Guid.NewGuid();

    private static Domain.Requests.Request Draft(bool fromInitiate)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            "03", "SIBS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, PriorId, null, null, null, null, "69000001", 1m, null)));
        if (fromInitiate)
            request.MarkAsPeriodicalReappraisal("G-1", "69000001", "60000001", "Y");
        return request;
    }

    // The purpose guard lives inside Save, so every caller is covered; it is exercised through Save.
    private static void SavePurpose(Domain.Requests.Request request, string purpose) =>
        request.Save(new RequestData(
            purpose, "SIBS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));

    [Fact]
    public void An_Initiate_draft_is_locked_and_a_manual_draft_is_not()
    {
        Assert.True(Draft(fromInitiate: true).ArePurposeAndPriorLocked());
        Assert.False(Draft(fromInitiate: false).ArePurposeAndPriorLocked());
    }

    [Fact]
    public void Changing_the_purpose_of_an_Initiate_draft_throws()
        => Assert.Throws<DomainException>(() => SavePurpose(Draft(true), "02"));

    private static Domain.Requests.Request LegacyRequest(bool fromInitiate, bool submitted)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            "03", "SIBS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, null, null, null, null, null, "99A0001234", 1m, new DateTime(2026, 1, 1))));
        if (fromInitiate)
            request.MarkAsPeriodicalReappraisal("G-1", "99A0001234", "60000001", "Y");
        if (submitted)
            request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));
        return request;
    }

    // The clear signal ("no value and date sent back") only counts on an unsubmitted Initiate draft; a submitted
    // request keeps its established rule (an omitted number on a legacy book is not a change).
    [Fact]
    public void A_legacy_clear_signal_throws_on_an_Initiate_draft()
        => Assert.Throws<DomainException>(() =>
            LegacyRequest(fromInitiate: true, submitted: false).EnsurePriorUnchanged(null, null, legacyPriorCleared: true));

    [Fact]
    public void A_legacy_clear_signal_is_ignored_on_a_submitted_request()
        => LegacyRequest(fromInitiate: false, submitted: true).EnsurePriorUnchanged(null, null, legacyPriorCleared: true);

    [Fact]
    public void A_legacy_clear_signal_is_ignored_on_a_manual_draft()
        => LegacyRequest(fromInitiate: false, submitted: false).EnsurePriorUnchanged(null, null, legacyPriorCleared: true);

    [Fact]
    public void Changing_the_prior_of_an_Initiate_draft_throws()
        => Assert.Throws<DomainException>(() => Draft(true).EnsurePriorUnchanged(Guid.NewGuid(), null));

    [Fact]
    public void Clearing_a_CAS_prior_of_an_Initiate_draft_throws()
        => Assert.Throws<DomainException>(() => Draft(true).EnsurePriorUnchanged(null, null));

    [Fact]
    public void Resending_the_same_purpose_and_prior_passes()
    {
        var request = Draft(true);

        SavePurpose(request, "03");
        request.EnsurePriorUnchanged(PriorId, null);
        request.EnsurePriorUnchanged(null, "69000001");   // LOS-style: the number alone, matching the stored one
    }

    [Fact]
    public void A_manual_draft_can_still_change_both()
    {
        var request = Draft(false);

        SavePurpose(request, "02");
        request.EnsurePriorUnchanged(Guid.NewGuid(), null);
        request.EnsurePriorUnchanged(null, null);
    }
}
