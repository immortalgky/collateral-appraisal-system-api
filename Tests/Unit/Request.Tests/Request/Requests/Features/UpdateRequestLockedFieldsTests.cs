using Appraisal.Contracts.Appraisals;
using Auth.Contracts.Users;
using MediatR;
using NSubstitute;
using Request.Application.Features.Requests.UpdateRequest;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.Exceptions;
using Shared.Models;

namespace Request.Tests.Request.Requests.Features;

/// <summary>
/// Once a request is submitted its purpose and prior appraisal are locked (the appraisal is built from
/// them at first submit and never re-based). The screen resends them unchanged, which must still pass.
/// DomainException is what CustomExceptionHandler maps to HTTP 400.
/// </summary>
public class UpdateRequestLockedFieldsTests
{
    private static readonly Guid PriorId = Guid.NewGuid();
    private static readonly Guid OtherPriorId = Guid.NewGuid();

    private const string LegacyBook = "99A0001234";

    /// <param name="useLegacy">stores a legacy book (<see cref="LegacyBook"/>) with no CAS id; otherwise the stored prior is <see cref="PriorId"/>.</param>
    /// <param name="lookupFindsPrior">false: the prior's CAS appraisal is gone (soft-deleted), so the lookup returns null.</param>
    private static (UpdateRequestCommandHandler Handler, Domain.Requests.Request Request, ISender Mediator) Setup(
        bool submitted, bool useLegacy = false, string? storedNumber = "69000001",
        string purpose = "02", bool lookupFindsPrior = true)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            purpose, "LOS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, useLegacy ? null : PriorId, null, null, null, null,
            useLegacy ? LegacyBook : storedNumber, 1m, new DateTime(2026, 1, 1))));
        if (submitted)
            request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        var repository = Substitute.For<IRequestRepository>();
        repository.GetByIdWithDocumentsAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var id = ci.Arg<GetAppraisalReferenceQuery>().AppraisalId;
            if (id == PriorId)
                return lookupFindsPrior ? new AppraisalReferenceResult("69000777", 7m, new DateTime(2026, 7, 7), "Completed") : null;
            return new AppraisalReferenceResult("69000009", 9m, new DateTime(2026, 2, 2), "Completed");
        });

        var userLookup = Substitute.For<IUserLookupService>();
        userLookup.GetRequestorAsync("u", Arg.Any<CancellationToken>())
            .Returns(new RequestorInfoDto(Guid.NewGuid(), "u", "U", null, null, null, null, null, null));

        return (new UpdateRequestCommandHandler(repository, Substitute.For<IRequestSyncService>(), mediator, userLookup),
            request, mediator);
    }

    private static UpdateRequestCommand Command(
        Guid requestId, string purpose, Guid? prevId, decimal? prevValue = null, DateTime? prevDate = null,
        string? prevNumber = null) =>
        new(requestId, purpose, "LOS", "u", new UserInfoDto("u", "U"), "Normal", false,
            new RequestDetailDto(
                false,
                new LoanDetailDto("IBG", "LA-1", 1_000_000m, null, null, null),
                prevId,
                new AddressDto("1", null, null, null, null, "sub", "dist", "prov", "10000"),
                new ContactDto("John", "0812345678", null),
                new AppointmentDto(new DateTime(2026, 9, 1), "site"),
                new FeeDto("Bank", null, null),
                prevNumber,
                PrevAppraisalValue: prevValue,
                PrevAppraisalDate: prevDate),
            null, null, null, null);

    [Fact]
    public async Task Submitted_request_with_a_changed_purpose_is_rejected()
    {
        var (handler, request, _) = Setup(submitted: true);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(Command(request.Id, "03", PriorId), TestContext.Current.CancellationToken));

        Assert.Contains("purpose", ex.Message);
        Assert.Equal("02", request.Purpose);
    }

    [Fact]
    public async Task Submitted_request_purpose_is_compared_exactly_so_a_trailing_space_is_rejected()
    {
        var (handler, request, _) = Setup(submitted: true, purpose: "14");

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(Command(request.Id, "14 ", PriorId), TestContext.Current.CancellationToken));

        Assert.Equal("14", request.Purpose);
    }

    [Fact]
    public async Task Submitted_request_with_a_different_prior_id_is_rejected()
    {
        var (handler, request, _) = Setup(submitted: true);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(Command(request.Id, "02", OtherPriorId), TestContext.Current.CancellationToken));

        Assert.Contains("prior appraisal", ex.Message);
        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
    }

    [Fact]
    public async Task Submitted_request_with_a_cleared_prior_is_rejected()
    {
        var (handler, request, _) = Setup(submitted: true);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(Command(request.Id, "02", null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Submitted_request_resending_the_same_purpose_and_prior_passes_and_keeps_what_was_stored()
    {
        var (handler, request, _) = Setup(submitted: true);

        var result = await handler.Handle(
            Command(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("02", request.Purpose);
        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
        // The lookup now says 69000777 / 7 / 2026-07-07; the submitted request keeps what it was submitted with.
        Assert.Equal("69000001", request.Detail.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2026, 1, 1), request.Detail.PrevAppraisalDate);
    }

    [Fact]
    public async Task Submitted_request_never_looks_the_prior_up()
    {
        var (handler, request, mediator) = Setup(submitted: true);

        await handler.Handle(Command(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Draft_request_does_look_the_prior_up()
    {
        var (handler, request, mediator) = Setup(submitted: false);

        await handler.Handle(Command(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        await mediator.Received(1).Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submitted_request_same_id_passes_when_the_prior_appraisal_is_gone_and_is_not_wiped()
    {
        var (handler, request, _) = Setup(submitted: true, lookupFindsPrior: false);

        var result = await handler.Handle(
            Command(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
        Assert.Equal("69000001", request.Detail.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2026, 1, 1), request.Detail.PrevAppraisalDate);
    }

    [Fact]
    public async Task Submitted_request_stored_with_no_number_passes_once_the_id_resolves()
    {
        var (handler, request, _) = Setup(submitted: true, storedNumber: null);

        var result = await handler.Handle(
            Command(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Submitted_legacy_book_resent_as_loaded_passes()
    {
        var (handler, request, _) = Setup(submitted: true, useLegacy: true);

        var result = await handler.Handle(
            Command(request.Id, "02", null, prevValue: 1m, prevDate: new DateTime(2026, 1, 1)),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Equal(LegacyBook, request.Detail.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Submitted_legacy_book_with_value_and_date_omitted_passes_and_is_kept()
    {
        // A partial payload is not a "clear": a submitted request's prior is never edited from the form.
        var (handler, request, _) = Setup(submitted: true, useLegacy: true);

        var result = await handler.Handle(Command(request.Id, "02", null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(LegacyBook, request.Detail!.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2026, 1, 1), request.Detail.PrevAppraisalDate);
    }

    [Theory]
    [InlineData("99a0001234")]
    [InlineData(" 99A0001234")]
    [InlineData("B99A0001234")]
    public async Task Submitted_legacy_book_number_is_compared_normalised(string incoming)
    {
        var (handler, request, _) = Setup(submitted: true, useLegacy: true);

        var result = await handler.Handle(
            Command(request.Id, "02", null, prevNumber: incoming), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(LegacyBook, request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Submitted_legacy_book_sent_with_a_different_number_is_rejected()
    {
        var (handler, request, _) = Setup(submitted: true, useLegacy: true);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(Command(request.Id, "02", null, prevNumber: "99A0009999"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Submitted_legacy_book_replaced_by_a_cas_appraisal_is_rejected()
    {
        var (handler, request, _) = Setup(submitted: true, useLegacy: true);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(Command(request.Id, "02", OtherPriorId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Submitted_legacy_book_with_a_different_number_is_rejected_and_the_same_number_passes()
    {
        var (_, request, _) = Setup(submitted: true, useLegacy: true);

        request.EnsurePriorUnchanged(null, LegacyBook);
        request.EnsurePriorUnchanged(null, "b99a0001234");
        Assert.Throws<DomainException>(() => request.EnsurePriorUnchanged(null, "99A0009999"));
    }

    [Fact]
    public async Task Draft_request_can_still_change_purpose_and_prior()
    {
        var (handler, request, _) = Setup(submitted: false);

        await handler.Handle(Command(request.Id, "03", OtherPriorId), TestContext.Current.CancellationToken);

        Assert.Equal("03", request.Purpose);
        Assert.Equal(OtherPriorId, request.Detail!.PrevAppraisalId);
        Assert.Equal("69000009", request.Detail.PrevAppraisalNumber);
    }
}
