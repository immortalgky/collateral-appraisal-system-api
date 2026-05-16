namespace Request.Application.Services;

public interface IUpdateRequestService
{
    Task<Request.Domain.Requests.Request> ResubmitRequestAsync(ResubmitRequestData command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a request with its documents. Used by the followup-resubmit branch which needs
    /// the aggregate for document syncing without running the full Resubmit mutation.
    /// </summary>
    Task<Request.Domain.Requests.Request> GetByIdWithDocumentsAsync(Guid requestId,
        CancellationToken cancellationToken);
}
