using Mapster;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.RequestTitles;
using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

public class ResubmitRequestCommandHandler(
    IUpdateRequestService updateRequestService,
    IRequestSyncService syncService
) : ICommandHandler<ResubmitRequestCommand, ResubmitRequestResult>
{
    public async Task<ResubmitRequestResult> Handle(ResubmitRequestCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var resubmitRequestData = command.Adapt<ResubmitRequestData>();
            var request = await updateRequestService.ResubmitRequestAsync(resubmitRequestData, cancellationToken);

            if (command.Documents is not null)
                await syncService.SyncDocumentsAsync(request, command.Documents, cancellationToken);

            IReadOnlyList<RequestTitle> titles = [];
            if (command.Titles is not null)
                titles = await syncService.SyncTitlesAsync(command.RequestId, command.Titles, cancellationToken);

            request.Validate();
            foreach (var title in titles)
                title.Validate();

            return new ResubmitRequestResult(
                status: "Success",
                message: "Request initiated successfully."
            );

        }catch (Exception ex)
        {
            return new ResubmitRequestResult(
                status: "Error",
                message: "Request initiated failed",
                errorCode: ex.Message
            );
        }
    }
}


