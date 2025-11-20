namespace Request.Services;

public interface IRequestTitleService
{
    Task CreateRequestTitleAsync(Guid sessionId, Guid requestId, RequestTitleDto requestTitleDto, CancellationToken cancellation);
    Task CreateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken);
    
    Task DraftRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken);
    Task UpdateRequestTitleAsync(Guid sessionId, Guid requestId, RequestTitleDto requestTitleDto, CancellationToken cancellation);
    Task UpdateRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken);
    Task UpdateDraftRequestTitlesAsync(Guid sessionId, Guid requestId, List<RequestTitleDto> requestTitleDtos,
        CancellationToken cancellationToken);
}
public record DocumentLinkedEventDto(
    string EntityType,
    Guid EntityId,
    List<Guid> Documents
);
