namespace Request.Services;

public interface IRequestTitleService
{
    Task CreateRequestTitleAsync(RequestTitleDto requestTitleDto, CancellationToken cancellation);
    Task CreateRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken);
    
    Task DraftRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken);
    Task UpdateRequestTitleAsync(RequestTitleDto requestTitleDto, CancellationToken cancellation);
    Task UpdateRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos, CancellationToken cancellationToken);
    Task UpdateDraftRequestTitlesAsync(Guid requestId, List<RequestTitleDto> requestTitleDtos,
        CancellationToken cancellationToken);
}
public record DocumentLinkedEventDto(
    string EntityType,
    Guid EntityId,
    List<Guid> Documents
);
