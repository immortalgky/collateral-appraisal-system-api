namespace Request.Application.Services;

public interface ICreateRequestService
{
    Task<(Request.Domain.Requests.Request,List<RequestTitle>)> CreateRequestAsync(CreateRequestData data,
        CancellationToken cancellationToken);

    // Cohesive create + submit for callers that need both in one command. Persists the
    // aggregate to DB before raising RequestSubmittedEvent so in-process event handlers
    // (which read titles + documents from DB) see the committed state. Runs inside the
    // caller's transaction.
    Task<(Request.Domain.Requests.Request, List<RequestTitle>)> CreateAndSubmitRequestAsync(
        CreateRequestData data,
        DateTime submittedAt,
        string? externalCaseKey,
        CancellationToken cancellationToken);
}