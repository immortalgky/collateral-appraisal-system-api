namespace Integration.Application.Services;

public interface IWebhookService
{
    Task SendAsync(string systemCode, string eventType, object payload, CancellationToken cancellationToken = default);
}
