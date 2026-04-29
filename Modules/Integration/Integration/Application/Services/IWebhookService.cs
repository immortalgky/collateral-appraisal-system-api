namespace Integration.Application.Services;

public interface IWebhookService
{
    Task SendAsync(
        Guid eventId,
        string systemCode,
        string eventType,
        string externalCaseKey,
        DateTime occurredAt,
        object data,
        CancellationToken cancellationToken = default);
}
