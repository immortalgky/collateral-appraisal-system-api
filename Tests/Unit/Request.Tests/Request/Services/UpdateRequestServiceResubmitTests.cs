using Appraisal.Contracts.Appraisals;
using MediatR;
using NSubstitute;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.Models;
using Shared.Exceptions;

namespace Request.Tests.Request.Services;

/// <summary>A resubmit replaces the request data: the prior appraisal LOS sends is the prior, omitted means none.</summary>
public class UpdateRequestServiceResubmitTests
{
    private static readonly Guid PriorId = Guid.NewGuid();

    private static (UpdateRequestService Service, Domain.Requests.Request Request, ISender Mediator) Setup(Guid? storedPrior, string purpose = "02")
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            purpose, "LOS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, storedPrior, null, null, null, null, "69000001", 1m, null)));

        var repository = Substitute.For<IRequestRepository>();
        repository.GetByIdWithDocumentsAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new AppraisalReferenceResult("69000002", 2m, null, "Completed"));
        return (new UpdateRequestService(repository, mediator), request, mediator);
    }

    private static ResubmitRequestData Data(
        Guid requestId, string purpose, Guid? prevId, string? prevNumber = null,
        decimal? prevValue = null, DateTime? prevDate = null) =>
        new(requestId, purpose, "LOS", new UserInfoDto("u", "U"), new UserInfoDto("u", "U"), "Normal", false,
            new RequestDetailDto(false, null, prevId, null, null, null, null, prevNumber, prevValue, prevDate),
            [], [], null, null, null);

    [Fact]
    public async Task Purpose_07_resubmitted_without_a_prior_clears_the_stored_one_and_passes()
    {
        var (service, request, _) = Setup(storedPrior: PriorId);

        await service.ResubmitRequestAsync(Data(request.Id, "07", null), TestContext.Current.CancellationToken);

        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Null(request.Detail.PrevAppraisalNumber);
    }

    [Fact]
    public async Task Required_purpose_resubmitted_without_a_prior_is_rejected_not_kept_from_storage()
    {
        var (service, request, _) = Setup(storedPrior: PriorId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.ResubmitRequestAsync(Data(request.Id, "02", null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Id_sent_is_re_resolved_into_number_and_value()
    {
        var (service, request, _) = Setup(storedPrior: null);

        await service.ResubmitRequestAsync(Data(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
        Assert.Equal("69000002", request.Detail.PrevAppraisalNumber);
        Assert.Equal(2m, request.Detail.PrevAppraisalValue);
    }

    [Fact]
    public async Task Legacy_99A_number_sent_without_an_id_is_kept_as_the_legacy_book()
    {
        var (service, request, mediator) = Setup(storedPrior: null);

        await service.ResubmitRequestAsync(Data(request.Id, "03", null, "99A0001234"), TestContext.Current.CancellationToken);

        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Equal("99A0001234", request.Detail.PrevAppraisalNumber);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_guid_is_never_stored_as_the_prior_id()
    {
        var (service, request, mediator) = Setup(storedPrior: PriorId);

        await service.ResubmitRequestAsync(Data(request.Id, "07", Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Null(request.Detail!.PrevAppraisalId);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Purpose_07_resubmitted_with_a_number_and_no_id_is_rejected_by_the_guard()
    {
        var (service, request, _) = Setup(storedPrior: null);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.ResubmitRequestAsync(
            Data(request.Id, "07", null, "69000001"), TestContext.Current.CancellationToken));
        Assert.Contains("07", ex.Message);
    }

    [Fact]
    public async Task The_reference_resolved_for_the_detail_is_not_looked_up_again_by_the_guard()
    {
        var (service, request, mediator) = Setup(storedPrior: null);

        await service.ResubmitRequestAsync(Data(request.Id, "02", PriorId), TestContext.Current.CancellationToken);

        await mediator.Received(1).Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    // A request Initiate created for a COLLATREV book has its purpose ("03") and prior locked from the start,
    // like a submitted one: LOS may resend them but not change them.
    private static (UpdateRequestService Service, Domain.Requests.Request Request, ISender Mediator) PeriodicalSetup()
    {
        var setup = Setup(storedPrior: null, purpose: "03");
        setup.Request.SetLegacyPriorBook("62A00645", 1m, null);
        setup.Request.MarkAsPeriodicalReappraisal("G-1", "62A00645", null, null);
        return setup;
    }

    [Fact]
    public async Task A_periodical_reappraisal_resubmitted_with_its_own_non_99A_book_passes_and_keeps_it()
    {
        var (service, request, mediator) = PeriodicalSetup();

        await service.ResubmitRequestAsync(Data(request.Id, "03", null, "B62A00645"), TestContext.Current.CancellationToken);

        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Equal("62A00645", request.Detail.PrevAppraisalNumber);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    // The lock keeps the stored prior, but the submit-time guard still runs for an Initiate draft: a prior that was
    // deleted, or stopped being Completed, after Initiate is a 400 here too.
    [Theory]
    [InlineData("InProgress")]
    [InlineData(null)]   // the lookup finds nothing (deleted)
    public async Task A_periodical_reappraisal_with_a_CAS_prior_that_is_no_longer_usable_is_rejected_by_the_submit_guard(
        string? priorStatus)
    {
        var (service, request, mediator) = Setup(storedPrior: PriorId, purpose: "03");
        request.MarkAsPeriodicalReappraisal("G-1", "69000001", null, null);
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(priorStatus is null ? null : new AppraisalReferenceResult("69000001", 2m, null, priorStatus));

        await Assert.ThrowsAsync<BadRequestException>(() => service.ResubmitRequestAsync(
            Data(request.Id, "03", null, "69000001"), TestContext.Current.CancellationToken));
    }

    // LOS may resend the book, but not change what the draft was created with: the stored value and date stay.
    [Fact]
    public async Task A_legacy_periodical_draft_keeps_its_stored_value_and_date_when_LOS_sends_others()
    {
        var (service, request, _) = PeriodicalSetup();

        await service.ResubmitRequestAsync(
            Data(request.Id, "03", null, "B62A00645", prevValue: 99m, prevDate: new DateTime(2020, 2, 2)),
            TestContext.Current.CancellationToken);

        Assert.Equal("62A00645", request.Detail!.PrevAppraisalNumber);
        Assert.Equal(1m, request.Detail.PrevAppraisalValue);
        Assert.Null(request.Detail.PrevAppraisalDate);
    }

    [Fact]
    public async Task A_block_unit_draft_keeps_the_units_own_price_when_LOS_sends_another()
    {
        var (service, request, _) = Setup(storedPrior: PriorId, purpose: "03");
        request.MarkAsPeriodicalReappraisal("G-1", "69000001", "60000001", null);
        request.SetReappraisalPriorValue(500m, new DateTime(2025, 5, 5));

        await service.ResubmitRequestAsync(
            Data(request.Id, "03", PriorId, prevValue: 99m, prevDate: new DateTime(2020, 2, 2)),
            TestContext.Current.CancellationToken);

        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
        Assert.Equal(500m, request.Detail.PrevAppraisalValue);
        Assert.Equal(new DateTime(2025, 5, 5), request.Detail.PrevAppraisalDate);
    }

    [Fact]
    public async Task A_periodical_reappraisal_resubmitted_with_another_book_is_rejected()
    {
        var (service, request, _) = PeriodicalSetup();

        await Assert.ThrowsAsync<DomainException>(() => service.ResubmitRequestAsync(
            Data(request.Id, "03", null, "62A99999"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_periodical_reappraisal_resubmitted_without_a_book_but_with_its_value_keeps_the_stored_one()
    {
        var (service, request, _) = PeriodicalSetup();

        await service.ResubmitRequestAsync(
            Data(request.Id, "03", null, prevValue: 1m), TestContext.Current.CancellationToken);

        Assert.Equal("62A00645", request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task A_periodical_reappraisal_resubmitted_with_no_book_value_or_date_is_a_clear_and_is_rejected()
    {
        var (service, request, _) = PeriodicalSetup();

        await Assert.ThrowsAsync<DomainException>(() => service.ResubmitRequestAsync(
            Data(request.Id, "03", null), TestContext.Current.CancellationToken));
        Assert.Equal("62A00645", request.Detail!.PrevAppraisalNumber);
    }

    [Fact]
    public async Task A_periodical_reappraisal_with_a_CAS_prior_resubmitted_by_number_only_keeps_the_stored_id()
    {
        var (service, request, _) = Setup(storedPrior: PriorId, purpose: "03");
        request.MarkAsPeriodicalReappraisal("G-1", "69000001", null, null);

        await service.ResubmitRequestAsync(
            Data(request.Id, "03", null, "69000001"), TestContext.Current.CancellationToken);

        Assert.Equal(PriorId, request.Detail!.PrevAppraisalId);
    }

    [Fact]
    public async Task A_periodical_reappraisal_resubmitted_with_another_purpose_is_rejected()
    {
        var (service, request, _) = PeriodicalSetup();

        await Assert.ThrowsAsync<DomainException>(() => service.ResubmitRequestAsync(
            Data(request.Id, "02", null, "62A00645"), TestContext.Current.CancellationToken));
    }
}
