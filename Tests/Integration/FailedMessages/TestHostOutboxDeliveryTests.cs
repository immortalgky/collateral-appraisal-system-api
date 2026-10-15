using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Messaging.Services;

namespace Integration.FailedMessages;

/// <summary>
/// The shared integration test host must not run the real outbox delivery loops. They poll all six module
/// outbox tables, so a row a test seeds (Pending, or an orphaned Processing that the test then resets) can be
/// claimed out from under the assertion a few milliseconds later. No test relies on real delivery: tests that
/// need a published message put it on the bus directly.
/// </summary>
[Collection("Integration")]
public class TestHostOutboxDeliveryTests(IntegrationTestFixture fixture)
{
    [Fact]
    public void TestHost_RunsNoOutboxDeliveryServices()
    {
        var running = fixture.IntegrationTestWebApplicationFactory.Services
            .GetServices<IHostedService>()
            .Select(h => h.GetType())
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IntegrationEventDeliveryService<>))
            .Select(t => t.GetGenericArguments()[0].Name)
            .ToList();

        Assert.Empty(running);
    }
}
