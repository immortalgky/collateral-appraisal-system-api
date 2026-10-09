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

    private static (UpdateRequestService Service, Domain.Requests.Request Request, ISender Mediator) Setup(Guid? storedPrior)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            "02", "LOS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        request.SetDetail(RequestDetail.Create(new RequestDetailData(
            false, null, storedPrior, null, null, null, null, "69000001", 1m, null)));

        var repository = Substitute.For<IRequestRepository>();
        repository.GetByIdWithDocumentsAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new AppraisalReferenceResult("69000002", 2m, null, "Completed"));
        return (new UpdateRequestService(repository, mediator), request, mediator);
    }

    private static ResubmitRequestData Data(Guid requestId, string purpose, Guid? prevId, string? prevNumber = null) =>
        new(requestId, purpose, "LOS", new UserInfoDto("u", "U"), new UserInfoDto("u", "U"), "Normal", false,
            new RequestDetailDto(false, null, prevId, null, null, null, null, prevNumber),
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

    [Fact]
    public async Task A_periodical_reappraisal_resubmitted_with_its_own_non_99A_book_passes_and_keeps_it()
    {
        var (service, request, mediator) = Setup(storedPrior: null);
        request.MarkAsPeriodicalReappraisal("G-1", "62A00645");

        await service.ResubmitRequestAsync(Data(request.Id, "03", null, "B62A00645"), TestContext.Current.CancellationToken);

        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Equal("62A00645", request.Detail.PrevAppraisalNumber);
        await mediator.DidNotReceive().Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("62A99999")]
    public async Task A_periodical_reappraisal_resubmitted_without_its_book_is_rejected(string? number)
    {
        var (service, request, _) = Setup(storedPrior: null);
        request.MarkAsPeriodicalReappraisal("G-1", "62A00645");

        await Assert.ThrowsAsync<BadRequestException>(() => service.ResubmitRequestAsync(
            Data(request.Id, "03", null, number), TestContext.Current.CancellationToken));
    }
}
