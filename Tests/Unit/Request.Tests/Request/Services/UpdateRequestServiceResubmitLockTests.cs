using Appraisal.Contracts.Appraisals;
using MediatR;
using NSubstitute;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.Exceptions;
using Shared.Models;

namespace Request.Tests.Request.Services;

/// <summary>
/// The LOS data-fix resubmit may resend a submitted request's purpose and prior appraisal but not change them.
/// DomainException is what CustomExceptionHandler maps to HTTP 400.
/// </summary>
public class UpdateRequestServiceResubmitLockTests
{
    private static readonly Guid PriorId = Guid.NewGuid();
    private const string LegacyBook = "99A0001234";

    private static (UpdateRequestService Service, Domain.Requests.Request Request, ISender Mediator) Setup(
        bool submitted = true, bool legacy = false, string purpose = "02")
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            purpose, "LOS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, legacy ? null : PriorId, null, null, null, null,
            legacy ? LegacyBook : "69000001", 1m, new DateTime(2026, 1, 1))));
        if (submitted)
            request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        var repository = Substitute.For<IRequestRepository>();
        repository.GetByIdWithDocumentsAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var mediator = Substitute.For<ISender>();
        // What a fresh lookup would say; a locked request must never take it over its stored values.
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new AppraisalReferenceResult("69000777", 7m, new DateTime(2026, 7, 7), "Completed"));
        return (new UpdateRequestService(repository, mediator), request, mediator);
    }

    private static ResubmitRequestData Data(
        Guid requestId, string purpose, Guid? prevId, string? prevNumber = null, decimal? prevValue = null) =>
        new(requestId, purpose, "LOS", new UserInfoDto("u", "U"), new UserInfoDto("u", "U"), "Normal", false,
            new RequestDetailDto(false, null, prevId, null, null, null, null, prevNumber, prevValue, null),
            [], [], null, null, null);

    private static Task Resubmit(UpdateRequestService service, ResubmitRequestData data) =>
        service.ResubmitRequestAsync(data, TestContext.Current.CancellationToken);

    // ---- purpose

    [Fact]
    public async Task Changed_purpose_is_rejected_and_nothing_is_written()
    {
        var (service, request, _) = Setup();

        var ex = await Assert.ThrowsAsync<DomainException>(() => Resubmit(service, Data(request.Id, "03", PriorId)));

        Assert.Contains("purpose", ex.Message);
        Assert.Equal("02", request.Purpose);
    }

    [Fact]
    public async Task Purpose_with_a_trailing_space_is_a_change()
    {
        var (service, request, _) = Setup(purpose: "14");

        await Assert.ThrowsAsync<DomainException>(() => Resubmit(service, Data(request.Id, "14 ", PriorId)));
        Assert.Equal("14", request.Purpose);
    }

    // ---- CAS prior

    [Fact]
    public async Task Changed_prior_id_is_rejected()
    {
        var (service, request, _) = Setup();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Resubmit(service, Data(request.Id, "02", Guid.NewGuid())));

        Assert.Contains("prior appraisal", ex.Message);
        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
    }

    [Fact]
    public async Task Prior_omitted_on_a_request_that_has_a_cas_prior_is_rejected()
    {
        var (service, request, _) = Setup();

        await Assert.ThrowsAsync<DomainException>(() => Resubmit(service, Data(request.Id, "02", null)));
    }

    [Fact]
    public async Task Same_prior_id_passes_keeps_the_stored_fields_and_looks_nothing_up()
    {
        var (service, request, mediator) = Setup();

        await Resubmit(service, Data(request.Id, "02", PriorId, prevValue: 99m));

        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
        Assert.Equal("69000001", request.Detail.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2026, 1, 1), request.Detail.PrevAppraisalDate);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("69000001")]
    [InlineData(" 69000001 ")]
    [InlineData("B69000001")]
    public async Task Cas_prior_sent_by_number_only_matches_the_stored_number_and_keeps_the_stored_id(string number)
    {
        var (service, request, _) = Setup();

        await Resubmit(service, Data(request.Id, "02", null, number));

        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
        Assert.Equal("69000001", request.Detail.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Cas_prior_sent_by_a_different_number_is_rejected()
    {
        var (service, request, _) = Setup();

        await Assert.ThrowsAsync<DomainException>(() => Resubmit(service, Data(request.Id, "02", null, "69000999")));
    }

    // ---- legacy book

    [Theory]
    [InlineData("99A0001234")]
    [InlineData("99a0001234")]
    [InlineData(" 99A0001234")]
    [InlineData("B99A0001234")]
    public async Task Legacy_book_resent_in_any_format_passes_and_the_stored_fields_are_kept(string number)
    {
        var (service, request, mediator) = Setup(legacy: true);

        await Resubmit(service, Data(request.Id, "02", null, number, prevValue: 99m));

        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Equal(LegacyBook, request.Detail.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Legacy_book_with_the_number_omitted_passes_and_is_kept()
    {
        var (service, request, _) = Setup(legacy: true);

        await Resubmit(service, Data(request.Id, "02", null));

        Assert.Equal(LegacyBook, request.Detail!.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
    }

    [Fact]
    public async Task Legacy_book_sent_with_a_different_number_is_rejected()
    {
        var (service, request, _) = Setup(legacy: true);

        await Assert.ThrowsAsync<DomainException>(() => Resubmit(service, Data(request.Id, "02", null, "99A0009999")));
        Assert.Equal(LegacyBook, request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Legacy_book_replaced_by_a_cas_id_is_rejected()
    {
        var (service, request, _) = Setup(legacy: true);

        await Assert.ThrowsAsync<DomainException>(() => Resubmit(service, Data(request.Id, "02", PriorId)));
    }
}
