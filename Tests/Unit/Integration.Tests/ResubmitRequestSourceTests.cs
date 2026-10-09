using Integration.Application.Features.AppraisalRequests.ResubmitRequest;
using Integration.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Request.Application.Services;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
using Request.Domain.RequestTitles;
using Shared.Data.Outbox;
using Workflow.Contracts.DocumentFollowups;

namespace Integration.Tests;

/// <summary>
/// A data-fix resubmit takes each row's Source from the payload (whitelisted by ClientDocumentSource), the same
/// as create — it used to force "REQUEST" on every row, so a file LOS carried as PREV came out as REQUEST.
/// </summary>
public class ResubmitRequestSourceTests
{
    private sealed class StopHere : Exception;

    [Fact]
    public async Task Data_fix_resubmit_trusts_the_payload_Source_instead_of_forcing_REQUEST()
    {
        var sync = Substitute.For<IRequestSyncService>();
        sync.SyncTitlesAsync(Arg.Any<Guid>(), Arg.Any<List<RequestTitleDto>>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(Task.FromException<IReadOnlyList<RequestTitle>>(new StopHere())); // ends the handler after both syncs

        var update = Substitute.For<IUpdateRequestService>();
        update.ResubmitRequestAsync(Arg.Any<ResubmitRequestData>(), Arg.Any<CancellationToken>())
            .Returns(Request.Domain.Requests.Request.Create(new Request.Domain.Requests.RequestData(
                "03", "LOS", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
                DateTime.Now, "Normal", false)));

        var handler = new ResubmitRequestCommandHandler(
            update, sync, Substitute.For<ISender>(), Substitute.For<IAppraisalLookupService>(),
            Substitute.For<IIntegrationEventOutbox>(), NullLogger<ResubmitRequestCommandHandler>.Instance);

        var user = new UserInfoDto("U1", "u1");
        var documents = new List<RequestDocumentDto>
        {
            new(null, Guid.Empty, Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null, "PREV", false, "u", "U", DateTime.Now)
        };
        var command = new ResubmitRequestCommand(
            Guid.NewGuid(), "03", "LOS", user, user, "Normal", false,
            new RequestDetailDto(false, null, Guid.NewGuid(), null, null, null, null, null, null, null),
            [], [], [], documents);

        await Assert.ThrowsAsync<StopHere>(() => handler.Handle(command, TestContext.Current.CancellationToken));

        await sync.Received(1).SyncDocumentsAsync(
            Arg.Any<Request.Domain.Requests.Request>(), documents, Arg.Any<CancellationToken>(), null);
        await sync.Received(1).SyncTitlesAsync(
            command.RequestId, command.Titles!, Arg.Any<CancellationToken>(), null);
    }

    [Fact]
    public async Task Data_fix_resubmit_of_a_reappraisal_draft_does_not_look_up_its_own_recorded_book()
    {
        var requestId = Guid.NewGuid();
        var draft = Request.Domain.Requests.Request.Create(new Request.Domain.Requests.RequestData(
            "03", "SIBS", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
            DateTime.Now, "Normal", false));
        draft.MarkAsPeriodicalReappraisal("G-1", "62A00645");
        var update = Substitute.For<IUpdateRequestService>();
        update.GetByIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>()).Returns(draft);
        update.ResubmitRequestAsync(Arg.Any<ResubmitRequestData>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Request.Domain.Requests.Request>(new StopHere()));
        var lookup = Substitute.For<IAppraisalLookupService>();

        var handler = new ResubmitRequestCommandHandler(
            update, Substitute.For<IRequestSyncService>(), Substitute.For<ISender>(), lookup,
            Substitute.For<IIntegrationEventOutbox>(), NullLogger<ResubmitRequestCommandHandler>.Instance);
        var user = new UserInfoDto("U1", "u1");
        var command = new ResubmitRequestCommand(
            requestId, "03", "SIBS", user, user, "Normal", false,
            new RequestDetailDto(false, null, null, null, null, null, null, "62A00645", null, null),
            [], [], null, null);

        await Assert.ThrowsAsync<StopHere>(() => handler.Handle(command, TestContext.Current.CancellationToken));

        // Reached the update service (so the resolver let the number through) without any CAS lookup.
        await lookup.DidNotReceive().ResolvePriorAppraisalByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await update.Received(1).ResubmitRequestAsync(
            Arg.Is<ResubmitRequestData>(d => d.Detail!.PrevAppraisalNumber == "62A00645"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Follow_up_resubmit_stamps_new_rows_FOLLOWUP_by_mode_not_by_the_client_label()
    {
        var requestId = Guid.NewGuid();
        var sync = Substitute.For<IRequestSyncService>();
        sync.SyncTitlesAsync(Arg.Any<Guid>(), Arg.Any<List<RequestTitleDto>>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(Task.FromException<IReadOnlyList<RequestTitle>>(new StopHere()));
        var update = Substitute.For<IUpdateRequestService>();
        update.GetByIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(Request.Domain.Requests.Request.Create(new Request.Domain.Requests.RequestData(
                "03", "LOS", new Shared.Models.UserInfo("u", "U"), new Shared.Models.UserInfo("u", "U"),
                DateTime.Now, "Normal", false)));
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetOpenDocumentFollowupForRequestQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<DocumentFollowupExternalDto>)
                [new DocumentFollowupExternalDto(Guid.NewGuid(), requestId, "Open", Guid.NewGuid(), [])]);

        var handler = new ResubmitRequestCommandHandler(
            update, sync, mediator, Substitute.For<IAppraisalLookupService>(),
            Substitute.For<IIntegrationEventOutbox>(), NullLogger<ResubmitRequestCommandHandler>.Instance);

        // The client labels its new file REQUEST (or nothing at all): in follow-up mode it still answers the follow-up.
        var documents = new List<RequestDocumentDto>
        {
            new(null, Guid.Empty, Guid.NewGuid(), "D001", "a.pdf", null, 1, null, null, "REQUEST", false, "u", "U", DateTime.Now)
        };
        var command = new ResubmitRequestCommand(
            requestId, null, null, null, null, null, null, null, null, null, [], documents, Mode: "Followup");

        await Assert.ThrowsAsync<StopHere>(() => handler.Handle(command, TestContext.Current.CancellationToken));

        await sync.Received(1).SyncDocumentsAsync(
            Arg.Any<Request.Domain.Requests.Request>(), documents, Arg.Any<CancellationToken>(), "FOLLOWUP");
        await sync.Received(1).SyncTitlesAsync(requestId, command.Titles!, Arg.Any<CancellationToken>(), "FOLLOWUP");
    }
}
