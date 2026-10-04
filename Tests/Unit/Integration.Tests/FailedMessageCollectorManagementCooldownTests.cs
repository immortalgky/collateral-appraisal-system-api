using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// After a 401/403/unreachable Management API result, Discover/Snapshot must skip
/// the Management API entirely for 10 minutes rather than re-proving it down every 15s round. Drives
/// <see cref="FailedMessageCollectorService.RecordManagementStatus"/>/<see cref="FailedMessageCollectorService.IsManagementCoolingDown"/>
/// directly — the seam extracted for exactly this — no broker or HTTP call needed.
/// </summary>
public class FailedMessageCollectorManagementCooldownTests
{
    private static FailedMessageCollectorService CreateCollector(ILogger<FailedMessageCollectorService>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        return new FailedMessageCollectorService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            logger ?? Substitute.For<ILogger<FailedMessageCollectorService>>(),
            Substitute.For<IDateTimeProvider>(),
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());
    }

    /// <summary>Minimal capturing logger (same pattern as
    /// Shared.Tests/Configuration/PlaintextSecretAuditTests.cs) — records the level of every entry so a
    /// test can assert on it without wrestling with ILogger's extension-method call shape.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    [Fact]
    public void RecordManagementStatus_Unauthorized_StartsATenMinuteCooldown()
    {
        var collector = CreateCollector();
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        collector.RecordManagementStatus(BrokerManagementStatus.Unauthorized, now);

        collector.IsManagementCoolingDown(now).Should().BeTrue();
        collector.IsManagementCoolingDown(now.AddMinutes(9)).Should().BeTrue();
        collector.IsManagementCoolingDown(now.AddMinutes(10).AddSeconds(1)).Should().BeFalse("the 10-minute cooldown has elapsed");
        collector.CachedManagementStatus.Should().Be(BrokerManagementStatus.Unauthorized);
    }

    [Fact]
    public void RecordManagementStatus_Ok_ClearsAnyCooldown()
    {
        var collector = CreateCollector();
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        collector.RecordManagementStatus(BrokerManagementStatus.Unreachable, now);
        collector.IsManagementCoolingDown(now).Should().BeTrue();

        collector.RecordManagementStatus(BrokerManagementStatus.Ok, now.AddMinutes(1));

        collector.IsManagementCoolingDown(now.AddMinutes(1)).Should().BeFalse();
        collector.CachedManagementStatus.Should().Be(BrokerManagementStatus.Ok);
    }

    [Fact]
    public void RecordManagementStatus_SameFailureAgain_KeepsExtendingTheCooldown()
    {
        var collector = CreateCollector();
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        collector.RecordManagementStatus(BrokerManagementStatus.Unreachable, now);
        // A later round still down re-records the same status — the cooldown window slides forward
        // rather than expiring on the original timer.
        collector.RecordManagementStatus(BrokerManagementStatus.Unreachable, now.AddMinutes(9));

        collector.IsManagementCoolingDown(now.AddMinutes(10)).Should().BeTrue("the cooldown was re-armed at +9min, not left at the original +10min");
    }

    [Fact]
    public void RecordManagementStatus_ChangeToOk_LogsAtInformation()
    {
        var logger = new CapturingLogger<FailedMessageCollectorService>();
        var collector = CreateCollector(logger);
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        collector.RecordManagementStatus(BrokerManagementStatus.Unreachable, now);
        collector.RecordManagementStatus(BrokerManagementStatus.Ok, now.AddMinutes(1));

        logger.Levels.Should().ContainInOrder(LogLevel.Warning, LogLevel.Information);
    }

    [Theory]
    [InlineData(BrokerManagementStatus.Unauthorized)]
    [InlineData(BrokerManagementStatus.Unreachable)]
    public void RecordManagementStatus_ChangeToFailureStatus_LogsAtWarning(string status)
    {
        var logger = new CapturingLogger<FailedMessageCollectorService>();
        var collector = CreateCollector(logger);
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        collector.RecordManagementStatus(status, now);

        logger.Levels.Should().ContainSingle().Which.Should().Be(LogLevel.Warning);
    }
}
