using Auth.Contracts.Users;
using Integration.Application.Features.AppraisalRequests.CreateRequest;
using Integration.Application.Features.AppraisalRequests.ResubmitRequest;
using Integration.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// LOS must send a Requestor (the RM) who is a registered, active CAS user, otherwise the emails that look the RM
/// up by employee code skip them silently. Both Integration handlers reject it with a 400 before any other work.
/// </summary>
public class RequestorGuardTests
{
    private sealed class StopHere : Exception;

    private static readonly UserInfoDto Rm = new("P5229", "p5229");

    /// <summary>A lookup that knows every employee code (the guard passes).</summary>
    internal static IUserLookupService KnownUsers()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetRequestorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RequestorInfoDto(Guid.NewGuid(), ci.ArgAt<string>(0), "RM", null, null, null, null, null, null));
        return lookup;
    }

    private static IUserLookupService UnknownUsers()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetRequestorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RequestorInfoDto?)null);
        return lookup;
    }

    private static CreateRequestCommand CreateCommand(RequestDetailDto? detail = null) =>
        new("03", "LOS", Rm, Rm, "Normal", false, detail, null, null, null, null, null);

    private static ResubmitRequestCommand ResubmitCommand() =>
        new(Guid.NewGuid(), "03", "LOS", Rm, Rm, "Normal", false,
            new RequestDetailDto(false, null, null, null, null, null, null, null, null, null), [], [], null, null);

    [Fact]
    public async Task Create_rejects_an_unregistered_or_inactive_requestor_before_any_other_work()
    {
        var create = Substitute.For<ICreateRequestService>();
        var validator = Substitute.For<IRequestDocumentValidator>();
        // A prior number the resolver would look up (and accept), so each later step is reachable if the guard moved below it.
        var lookup = Substitute.For<IAppraisalLookupService>();
        lookup.ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PriorAppraisalRef(Guid.NewGuid(), "Completed"));
        var handler = new CreateRequestCommandHandler(
            create, validator, lookup, Substitute.For<IDateTimeProvider>(), UnknownUsers());
        var detail = new RequestDetailDto(false, null, null, null, null, null, null, "62A00645", null, null);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(CreateCommand(detail), TestContext.Current.CancellationToken));

        Assert.Contains("Requestor 'P5229' is not registered or is inactive in CAS.", ex.Message);
        await create.DidNotReceiveWithAnyArgs().CreateAndSubmitRequestAsync(default!, default, default, default, TestContext.Current.CancellationToken);
        await validator.DidNotReceiveWithAnyArgs().ValidateAsync(default!, TestContext.Current.CancellationToken);
        await lookup.DidNotReceiveWithAnyArgs().ResolvePriorAppraisalByNumberAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Create_with_a_known_requestor_proceeds_to_the_create_service()
    {
        var create = Substitute.For<ICreateRequestService>();
        create.CreateAndSubmitRequestAsync(default!, default, default, default, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(Task.FromException<(Request.Domain.Requests.Request, List<Request.Domain.RequestTitles.RequestTitle>)>(new StopHere()));
        var handler = new CreateRequestCommandHandler(
            create, Substitute.For<IRequestDocumentValidator>(), Substitute.For<IAppraisalLookupService>(),
            Substitute.For<IDateTimeProvider>(), KnownUsers());

        await Assert.ThrowsAsync<StopHere>(() => handler.Handle(CreateCommand(), TestContext.Current.CancellationToken));

        await create.ReceivedWithAnyArgs(1).CreateAndSubmitRequestAsync(default!, default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Data_fix_resubmit_rejects_an_unregistered_or_inactive_requestor_before_any_other_work()
    {
        var update = Substitute.For<IUpdateRequestService>();
        var handler = new ResubmitRequestCommandHandler(
            update, Substitute.For<IRequestSyncService>(), Substitute.For<ISender>(),
            Substitute.For<IAppraisalLookupService>(), Substitute.For<IIntegrationEventOutbox>(),
            NullLogger<ResubmitRequestCommandHandler>.Instance, UnknownUsers());

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(ResubmitCommand(), TestContext.Current.CancellationToken));

        Assert.Contains("Requestor 'P5229' is not registered or is inactive in CAS.", ex.Message);
        await update.DidNotReceiveWithAnyArgs().GetByIdWithDocumentsAsync(default, TestContext.Current.CancellationToken);
        await update.DidNotReceiveWithAnyArgs().ResubmitRequestAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Data_fix_resubmit_with_a_known_requestor_proceeds_to_the_update_service()
    {
        var update = Substitute.For<IUpdateRequestService>();
        update.ResubmitRequestAsync(default!, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(Task.FromException<Request.Domain.Requests.Request>(new StopHere()));
        var handler = new ResubmitRequestCommandHandler(
            update, Substitute.For<IRequestSyncService>(), Substitute.For<ISender>(),
            Substitute.For<IAppraisalLookupService>(), Substitute.For<IIntegrationEventOutbox>(),
            NullLogger<ResubmitRequestCommandHandler>.Instance, KnownUsers());

        await Assert.ThrowsAsync<StopHere>(() => handler.Handle(ResubmitCommand(), TestContext.Current.CancellationToken));

        await update.ReceivedWithAnyArgs(1).ResubmitRequestAsync(default!, TestContext.Current.CancellationToken);
    }
}
