using System.Net.Http.Json;
using System.Text.Json;
using Integration.Application.Features.WebhookDeliveries.GetWebhookDeliverySummary;
using Integration.Application.Features.WebhookDeliveries.GetWebhookDelivery;
using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.Fixtures;
using Integration.Helpers;
using Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shared.Pagination;
using Shared.Time;

namespace Integration.Webhooks;

/// <summary>
/// The delivery list and its summary share <c>WebhookDeliveryFilter</c>; run them against real SQL so a bad
/// column or LIKE escape fails here. The shared database is never cleaned, so every test seeds its own
/// SystemCode and filters by it — counts are then exact, not deltas.
/// </summary>
public class WebhookDeliveriesApiTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static string NewSystemCode() => $"T{Guid.NewGuid():N}"[..10];

    private async Task<(string SystemCode, WebhookDelivery[] Deliveries)> SeedAsync(
        Func<Guid, WebhookDelivery[]> build, string callbackUrl = "https://example.test/hook")
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var systemCode = NewSystemCode();
        var subscription = WebhookSubscription.Create(systemCode, callbackUrl, "secret-key-for-tests-0123456789");
        db.WebhookSubscriptions.Add(subscription);
        var deliveries = build(subscription.Id);
        db.WebhookDeliveries.AddRange(deliveries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (systemCode, deliveries);
    }

    private static WebhookDelivery Pending(Guid sub, string eventType) =>
        WebhookDelivery.Create(sub, eventType, "{}");

    private static WebhookDelivery Failed(Guid sub, string eventType, int status)
    {
        var d = WebhookDelivery.Create(sub, eventType, "{}");
        d.RecordFailure(status, 3, "boom");
        return d;
    }

    private WebhookDelivery Delivered(Guid sub, string eventType)
    {
        var clock = Fixture.IntegrationTestWebApplicationFactory.Services.GetRequiredService<IDateTimeProvider>();
        var d = WebhookDelivery.Create(sub, eventType, "{}");
        d.RecordSuccess(200, 1, clock.ApplicationNow);
        return d;
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"{url} failed: {(int)response.StatusCode} {raw}");
        var result = JsonSerializer.Deserialize<T>(raw, JsonHelper.Options);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task Summary_CountsBreakdownsAndOldestPending_ForTheFilteredSystem()
    {
        var (system, _) = await SeedAsync(sub =>
        [
            Pending(sub, "APPRAISAL_CREATED"),
            Pending(sub, "APPRAISAL_CREATED"),
            Delivered(sub, "APPRAISAL_COMPLETED"),
            Failed(sub, "APPRAISAL_STATUS_CHANGED", 404),
            Failed(sub, "APPRAISAL_STATUS_CHANGED", 404),
            Failed(sub, "APPRAISAL_CANCELLED", 500)
        ], "https://user:pw@example.test/api?token=secret");

        var s = await GetAsync<WebhookDeliverySummaryDto>($"/webhook-deliveries/summary?systemCode={system}");

        Assert.Equal(6, s.Total);
        Assert.Equal(2, s.Pending);
        Assert.Equal(1, s.Delivered);
        Assert.Equal(3, s.Failed);

        Assert.NotNull(s.OldestPending);
        Assert.Equal(2, s.OldestPending.NeverAttemptedCount);
        Assert.Equal(new SystemCountDto(system, 2), Assert.Single(s.OldestPending.BySystem));

        Assert.Equal(
            [new EventTypeCountDto("APPRAISAL_STATUS_CHANGED", 2), new EventTypeCountDto("APPRAISAL_CANCELLED", 1)],
            s.FailedByEventType);
        Assert.Equal(
            [
                new FailureCauseDto(system, 404, "POST", "https://example.test/api", 2),
                new FailureCauseDto(system, 500, "POST", "https://example.test/api", 1)
            ],
            s.FailedByCause);
    }

    [Fact]
    public async Task Summary_NoPending_OldestPendingIsNull_AndStatusIsNotAFilter()
    {
        var (system, _) = await SeedAsync(sub => [Delivered(sub, "APPRAISAL_COMPLETED")]);

        var s = await GetAsync<WebhookDeliverySummaryDto>($"/webhook-deliveries/summary?systemCode={system}");

        Assert.Equal(1, s.Total);
        Assert.Null(s.OldestPending);
        Assert.Empty(s.FailedByEventType);
        Assert.Empty(s.FailedByCause);
    }

    [Fact]
    public async Task Summary_UnknownSystem_IsAllZeros()
    {
        var s = await GetAsync<WebhookDeliverySummaryDto>($"/webhook-deliveries/summary?systemCode={NewSystemCode()}");

        Assert.Equal(0, s.Total);
        Assert.Equal(0, s.Pending);
        Assert.Null(s.OldestPending);
    }

    [Fact]
    public async Task Summary_AndList_FilterIdentically_ByEventTypeAndSearch()
    {
        var (system, deliveries) = await SeedAsync(sub =>
        [
            Pending(sub, "APPRAISAL_CREATED"),
            Failed(sub, "APPRAISAL_STATUS_CHANGED", 404),
            Failed(sub, "QUOTATION_READY", 404)
        ]);

        var byEvent = await GetAsync<WebhookDeliverySummaryDto>(
            $"/webhook-deliveries/summary?systemCode={system}&eventType=QUOTATION_READY");
        Assert.Equal(1, byEvent.Total);

        // Text search is a case-insensitive substring of EventType.
        var bySearch = await GetAsync<WebhookDeliverySummaryDto>(
            $"/webhook-deliveries/summary?systemCode={system}&search=appraisal_");
        var listBySearch = await GetAsync<PaginatedResult<JsonElement>>(
            $"/webhook-deliveries?pageNumber=1&pageSize=50&systemCode={system}&search=appraisal_");
        Assert.Equal(2, bySearch.Total);
        Assert.Equal(bySearch.Total, listBySearch.Count);

        // "%" is a LIKE wildcard; typed literally it matches no event type.
        var wildcard = await GetAsync<WebhookDeliverySummaryDto>(
            $"/webhook-deliveries/summary?systemCode={system}&search=%25");
        Assert.Equal(0, wildcard.Total);

        // The subscription filter reaches both endpoints.
        var sub = deliveries[0].SubscriptionId;
        var bySub = await GetAsync<WebhookDeliverySummaryDto>($"/webhook-deliveries/summary?subscriptionId={sub}");
        var listBySub = await GetAsync<PaginatedResult<JsonElement>>(
            $"/webhook-deliveries?pageNumber=1&pageSize=50&subscriptionId={sub}");
        Assert.Equal(3, bySub.Total);
        Assert.Equal(3, listBySub.Count);

        // A Guid matches the delivery id, not the event type.
        var byId = await GetAsync<WebhookDeliverySummaryDto>(
            $"/webhook-deliveries/summary?systemCode={system}&search={deliveries[1].Id}");
        Assert.Equal(1, byId.Total);
        Assert.Equal(1, byId.Failed);
    }

    [Fact]
    public async Task List_StatusStillFilters_AndSystemCodeIsExact()
    {
        var (system, _) = await SeedAsync(sub => [Pending(sub, "APPRAISAL_CREATED"), Failed(sub, "APPRAISAL_CREATED", 500)]);

        var failed = await GetAsync<PaginatedResult<JsonElement>>(
            $"/webhook-deliveries?pageNumber=1&pageSize=50&systemCode={system}&status=Failed");
        Assert.Equal(1, failed.Count);

        var partialSystem = await GetAsync<PaginatedResult<JsonElement>>(
            $"/webhook-deliveries?pageNumber=1&pageSize=50&systemCode={system[..5]}");
        Assert.Equal(0, partialSystem.Count);
    }

    [Fact]
    public async Task Detail_IncludesSubscriptionConnectionFields()
    {
        var (_, deliveries) = await SeedAsync(sub => [Pending(sub, "APPRAISAL_CREATED")], "https://user:pw@example.test/detail?token=secret#f");

        var detail = await GetAsync<WebhookDeliveryDetailDto>($"/webhook-deliveries/{deliveries[0].Id}");

        Assert.Equal("https://example.test/detail", detail.CallbackUrl);
        Assert.Equal("POST", detail.HttpMethod);
        Assert.Equal("HMAC", detail.AuthType, ignoreCase: true);
        Assert.Null(detail.SubscriptionEventType);
    }

    [Fact]
    public async Task EventTypes_ListsTheTenConstants_Sorted()
    {
        var types = await GetAsync<string[]>("/webhook-subscriptions/event-types");

        Assert.Equal(WebhookEventTypes.All, types);
        Assert.Equal(10, types.Length);
        Assert.Equal(types.Order(StringComparer.Ordinal), types);
    }
}
