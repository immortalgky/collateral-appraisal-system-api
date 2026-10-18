using Appraisal.Contracts.Appraisals;
using Auth.Contracts.Users;
using MediatR;
using NSubstitute;
using Request.Application.Features.Requests.UpdateDraftRequest;
using Request.Application.Features.Requests.UpdateRequest;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.Exceptions;
using Shared.Models;

namespace Request.Tests.Request.Requests.Features;

/// <summary>
/// A request Initiate created for a COLLATREV book has its purpose and which prior appraisal it follows locked
/// from the start; resending the same values must still pass (the form re-sends the whole draft on every save).
/// The stored prior is kept as it is on every save: id, number, value and date (a block-project unit's own price
/// included), with no lookup in CAS.
/// </summary>
public class InitiateDraftHandlerLockTests
{
    private static readonly Guid PriorId = Guid.NewGuid();
    private static readonly Guid OtherPriorId = Guid.NewGuid();
    private const string LegacyBook = "99A0001234";

    private sealed record Fixture(
        UpdateRequestCommandHandler Update,
        UpdateDraftRequestCommandHandler Draft,
        Domain.Requests.Request Request,
        ISender Mediator,
        IUserLookupService UserLookup);

    private static Fixture Setup(
        bool initiate, bool legacy = false, bool lookupFindsPrior = true, bool submitted = false,
        bool storedValueAndDate = true)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            "03", "SIBS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, legacy ? null : PriorId, null, null, null, null,
            legacy ? LegacyBook : "69000001", storedValueAndDate ? 1m : null,
            storedValueAndDate ? new DateTime(2026, 1, 1) : null)));
        if (submitted)
            request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));
        if (initiate)
            request.MarkAsPeriodicalReappraisal("G-1", legacy ? LegacyBook : "69000001", "60000001", "Y");

        var repository = Substitute.For<IRequestRepository>();
        repository.GetByIdWithDocumentsAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(lookupFindsPrior ? new AppraisalReferenceResult("69000777", 7m, new DateTime(2026, 7, 7), "Completed") : null);
        var userLookup = Substitute.For<IUserLookupService>();
        userLookup.GetRequestorAsync("u", Arg.Any<CancellationToken>())
            .Returns(new RequestorInfoDto(Guid.NewGuid(), "u", "U", null, null, null, null, null, null));
        var sync = Substitute.For<IRequestSyncService>();

        return new Fixture(
            new UpdateRequestCommandHandler(repository, sync, mediator, userLookup),
            new UpdateDraftRequestCommandHandler(repository, sync, mediator, userLookup),
            request, mediator, userLookup);
    }

    private static RequestDetailDto Detail(Guid? prevId, decimal? value = null, DateTime? date = null) => new(
        false,
        new LoanDetailDto("IBG", "LA-1", 1_000_000m, null, null, null),
        prevId,
        new AddressDto("1", null, null, null, null, "sub", "dist", "prov", "10000"),
        new ContactDto("John", "0812345678", null),
        new AppointmentDto(new DateTime(2026, 9, 1), "site"),
        new FeeDto("Bank", null, null),
        null,
        PrevAppraisalValue: value,
        PrevAppraisalDate: date);

    private static UpdateRequestCommand Update(Guid id, string purpose, RequestDetailDto detail) =>
        new(id, purpose, "SIBS", "u", new UserInfoDto("u", "U"), "Normal", false, detail, null, null, null, null);

    private static UpdateDraftRequestCommand Draft(Guid id, string purpose, RequestDetailDto detail) =>
        new(id, purpose, "SIBS", "u", new UserInfoDto("u", "U"), "Normal", false, detail, null, null, null, null);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── Update (full save) ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_with_a_changed_purpose_throws()
    {
        var f = Setup(initiate: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Update.Handle(Update(f.Request.Id, "02", Detail(PriorId)), Ct));
        Assert.Equal("03", f.Request.Purpose);
    }

    [Fact]
    public async Task Update_with_a_changed_prior_throws()
    {
        var f = Setup(initiate: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Update.Handle(Update(f.Request.Id, "03", Detail(OtherPriorId)), Ct));
    }

    [Fact]
    public async Task Update_that_clears_a_CAS_prior_throws()
    {
        var f = Setup(initiate: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Update.Handle(Update(f.Request.Id, "03", Detail(null)), Ct));
    }

    [Fact]
    public async Task Update_that_clears_a_legacy_prior_throws_instead_of_doing_nothing()
    {
        var f = Setup(initiate: true, legacy: true);

        // The form signals "cleared" by sending back no id, no value and no date.
        await Assert.ThrowsAsync<DomainException>(() => f.Update.Handle(Update(f.Request.Id, "03", Detail(null)), Ct));
    }

    // Pins today's behaviour: a legacy book stored with neither value nor date cannot be "cleared" by the form's signal
    // (it sends back exactly those nulls on every save), so a save that sends no prior keeps the book and does not throw.
    [Fact]
    public async Task A_legacy_book_stored_without_value_or_date_is_kept_by_a_save_that_sends_no_prior()
    {
        var f = Setup(initiate: true, legacy: true, storedValueAndDate: false);

        var updated = await f.Update.Handle(Update(f.Request.Id, "03", Detail(null)), Ct);
        var drafted = await f.Draft.Handle(Draft(f.Request.Id, "03", Detail(null)), Ct);

        Assert.True(updated.IsSuccess);
        Assert.True(drafted.IsSuccess);
        Assert.Equal(LegacyBook, f.Request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Update_resending_the_same_purpose_and_a_legacy_prior_passes_and_keeps_it()
    {
        var f = Setup(initiate: true, legacy: true);

        var result = await f.Update.Handle(
            Update(f.Request.Id, "03", Detail(null, 1m, new DateTime(2026, 1, 1))), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(LegacyBook, f.Request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Update_resending_the_same_values_passes_and_keeps_the_value_Initiate_set()
    {
        var f = Setup(initiate: true);
        // A block-project unit: Initiate stored the unit's own price, not the project's.
        f.Request.SetReappraisalPriorValue(500m, new DateTime(2025, 5, 5));

        var result = await f.Update.Handle(Update(f.Request.Id, "03", Detail(PriorId)), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriorId, f.Request.Detail!.PrevAppraisalId);
        Assert.Equal(500m, f.Request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2025, 5, 5), f.Request.Detail.PrevAppraisalDate);
    }

    [Fact]
    public async Task Update_of_an_Initiate_draft_whose_prior_was_deleted_since_keeps_the_stored_prior()
    {
        var f = Setup(initiate: true, lookupFindsPrior: false);
        f.Request.SetReappraisalPriorValue(500m, new DateTime(2025, 5, 5));

        await f.Update.Handle(Update(f.Request.Id, "03", Detail(PriorId)), Ct);

        Assert.Equal(PriorId, f.Request.Detail!.PrevAppraisalId);
        Assert.Equal("69000001", f.Request.Detail.PrevAppraisalNumber);
        Assert.Equal(500m, f.Request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2025, 5, 5), f.Request.Detail.PrevAppraisalDate);
    }

    [Fact]
    public async Task Update_and_draft_save_of_an_Initiate_draft_with_an_unchanged_CAS_prior_make_no_CAS_lookup()
    {
        var f = Setup(initiate: true);

        await f.Update.Handle(Update(f.Request.Id, "03", Detail(PriorId)), Ct);
        await f.Draft.Handle(Draft(f.Request.Id, "03", Detail(PriorId)), Ct);

        await f.Mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
        Assert.Equal("69000001", f.Request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task A_manual_draft_can_change_purpose_and_prior_and_takes_the_looked_up_value()
    {
        var f = Setup(initiate: false);

        var result = await f.Update.Handle(Update(f.Request.Id, "02", Detail(OtherPriorId)), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("02", f.Request.Purpose);
        Assert.Equal(OtherPriorId, f.Request.Detail!.PrevAppraisalId);
        Assert.Equal(7m, f.Request.Detail.PrevAppraisalValue);
    }

    // ── Save as draft ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Draft_save_of_a_submitted_request_is_rejected_before_any_lookup_runs()
    {
        var f = Setup(initiate: false, submitted: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Draft.Handle(Draft(f.Request.Id, "03", Detail(PriorId)), Ct));

        await f.UserLookup.DidNotReceive().GetRequestorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await f.Mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Draft_save_with_a_changed_purpose_throws()
    {
        var f = Setup(initiate: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Draft.Handle(Draft(f.Request.Id, "02", Detail(PriorId)), Ct));
        Assert.Equal("03", f.Request.Purpose);
    }

    [Fact]
    public async Task Draft_save_of_an_Initiate_draft_that_sends_no_Detail_keeps_the_stored_prior()
    {
        var f = Setup(initiate: true);

        var result = await f.Draft.Handle(
            new UpdateDraftRequestCommand(f.Request.Id, "03", "SIBS", "u", new UserInfoDto("u", "U"), "Normal", false,
                null, null, null, null, null), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriorId, f.Request.Detail!.PrevAppraisalId);
        Assert.Equal("69000001", f.Request.Detail.PrevAppraisalNumber);
        Assert.Equal(1m, f.Request.Detail.PrevAppraisalValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Draft_save_of_an_Initiate_draft_without_a_purpose_keeps_the_stored_one(string? purpose)
    {
        var f = Setup(initiate: true);

        var result = await f.Draft.Handle(Draft(f.Request.Id, purpose!, Detail(PriorId)), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("03", f.Request.Purpose);
    }

    [Fact]
    public async Task Draft_save_with_a_changed_prior_throws()
    {
        var f = Setup(initiate: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Draft.Handle(Draft(f.Request.Id, "03", Detail(OtherPriorId)), Ct));
    }

    [Fact]
    public async Task Draft_save_that_clears_a_legacy_prior_throws()
    {
        var f = Setup(initiate: true, legacy: true);

        await Assert.ThrowsAsync<DomainException>(() => f.Draft.Handle(Draft(f.Request.Id, "03", Detail(null)), Ct));
    }

    [Fact]
    public async Task Draft_save_resending_the_same_values_passes()
    {
        var f = Setup(initiate: true);

        var result = await f.Draft.Handle(Draft(f.Request.Id, "03", Detail(PriorId)), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriorId, f.Request.Detail!.PrevAppraisalId);
    }
}
