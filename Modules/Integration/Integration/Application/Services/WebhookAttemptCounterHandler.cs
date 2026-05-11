namespace Integration.Application.Services;

/// <summary>
/// Counts how many times a request is actually sent to the wire (including resilience retries).
/// Register this AFTER AddStandardResilienceHandler() so each retry re-enters this handler.
/// The count is stored on HttpRequestMessage.Options under AttemptCountKey.
/// </summary>
public class WebhookAttemptCounterHandler : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<int> AttemptCountKey = new("webhook.attempt_count");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Options.TryGetValue(AttemptCountKey, out var current);
        request.Options.Set(AttemptCountKey, current + 1);

        return await base.SendAsync(request, cancellationToken);
    }
}
