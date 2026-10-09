using FluentAssertions;
using Integration.FailedMessages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Integration.Tests;

/// <summary>FailedMessagesOptions.Validate() — same ValidateOnStart/PostConfigure
/// pattern as Shared.Configurations.BackgroundJobsOptions, so a bad cadence fails fast at host startup.</summary>
public class FailedMessagesOptionsValidationTests
{
    [Fact]
    public void Validate_Defaults_DoesNotThrow() =>
        new FailedMessagesOptions { Enabled = true }.Invoking(o => o.Validate()).Should().NotThrow();

    // A disabled collector never uses any of these values, so a bad one must not block API startup.
    [Fact]
    public void Validate_Disabled_InvalidValues_DoNotThrow() =>
        new FailedMessagesOptions
            {
                Enabled = false,
                Interval = TimeSpan.Zero,
                BatchPerQueue = 0,
                ManagementUrl = "http://rabbit.internal.example:15672"
            }
            .Invoking(o => o.Validate()).Should().NotThrow();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IntervalNotPositive_Throws(int seconds)
    {
        var options = new FailedMessagesOptions { Enabled = true, Interval = TimeSpan.FromSeconds(seconds) };

        options.Invoking(o => o.Validate()).Should()
            .Throw<InvalidOperationException>().WithMessage("FailedMessages:Interval must be positive");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_BatchPerQueueNotPositive_Throws(int batchPerQueue)
    {
        var options = new FailedMessagesOptions { Enabled = true, BatchPerQueue = batchPerQueue };

        options.Invoking(o => o.Validate()).Should()
            .Throw<InvalidOperationException>().WithMessage("FailedMessages:BatchPerQueue must be at least 1");
    }

    // The Skipped-row purge runs from the cleanup job whether or not the collector is enabled.
    [Fact]
    public void SkippedPendingRetentionDays_DefaultsTo30() =>
        new FailedMessagesOptions().SkippedPendingRetentionDays.Should().Be(30);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_SkippedPendingRetentionDaysPositive_DoesNotThrow(bool enabled) =>
        new FailedMessagesOptions { Enabled = enabled, SkippedPendingRetentionDays = 1 }
            .Invoking(o => o.Validate()).Should().NotThrow();

    // Validated even while Enabled=false: the cleanup job is registered regardless of the collector, and a
    // zero/negative value would put the cutoff at "now" (or in the future) and purge every Pending Skipped row.
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(false, 0)]
    [InlineData(false, -30)]
    public void Validate_SkippedPendingRetentionDaysNotPositive_Throws(bool enabled, int days) =>
        new FailedMessagesOptions { Enabled = enabled, SkippedPendingRetentionDays = days }
            .Invoking(o => o.Validate()).Should()
            .Throw<InvalidOperationException>()
            .WithMessage("FailedMessages:SkippedPendingRetentionDays must be between 1 and 3650");

    // DateTime.AddDays(-days) throws ArgumentOutOfRangeException once the cutoff leaves DateTime's range, which
    // would kill the whole nightly job (the 90-day and snapshot purges too) before any DELETE ran.
    [Theory]
    [InlineData(true, 3651)]
    [InlineData(false, 3651)]
    [InlineData(false, 999999)]
    [InlineData(true, int.MaxValue)]
    public void Validate_SkippedPendingRetentionDaysTooLarge_Throws(bool enabled, int days) =>
        new FailedMessagesOptions { Enabled = enabled, SkippedPendingRetentionDays = days }
            .Invoking(o => o.Validate()).Should().Throw<InvalidOperationException>()
            .WithMessage("FailedMessages:SkippedPendingRetentionDays must be between 1 and 3650");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_SkippedPendingRetentionDaysAtUpperBound_DoesNotThrow(bool enabled) =>
        new FailedMessagesOptions { Enabled = enabled, SkippedPendingRetentionDays = 3650 }
            .Invoking(o => o.Validate()).Should().NotThrow();

    [Fact]
    public void IntegrationModule_DisabledWithBadSkippedRetention_FailsOnResolve() =>
        FluentActions.Invoking(() => ResolveOptions(enabled: false, null, legacy: null, skippedRetention: "0"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("FailedMessages:SkippedPendingRetentionDays must be between 1 and 3650");

    [Fact]
    public void IntegrationModule_ReadsSkippedRetentionFromConfiguration() =>
        ResolveOptions(enabled: false, null, legacy: null, skippedRetention: "7").SkippedPendingRetentionDays
            .Should().Be(7);

    // The app's AMQP username/password are re-sent as HTTP Basic auth to the Management API every round, so
    // a plain-http URL to a remote host would put the RabbitMQ password on the wire in cleartext.
    [Theory]
    [InlineData("http://localhost:15672")]
    [InlineData("http://LOCALHOST:15672/rabbit")]
    [InlineData("http://127.0.0.1:15672")]
    [InlineData("http://[::1]:15672")]
    [InlineData("https://localhost:15671")]
    [InlineData("https://rabbit.internal.example:15671")]
    [InlineData("HTTPS://rabbit.internal.example")]
    public void Validate_ManagementUrl_HttpsAnywhereOrHttpLoopback_DoesNotThrow(string url) =>
        new FailedMessagesOptions { Enabled = true, ManagementUrl = url }.Invoking(o => o.Validate()).Should().NotThrow();

    [Theory]
    [InlineData("http://rabbit.internal.example:15672")]
    [InlineData("http://10.0.0.5:15672")]
    [InlineData("HTTP://rabbit.internal.example")]
    [InlineData("http://localhost.evil.example:15672")]
    [InlineData("http://localhost@evil.example:15672")]
    public void Validate_ManagementUrl_HttpToRemoteHost_Throws(string url) =>
        new FailedMessagesOptions { Enabled = true, ManagementUrl = url }.Invoking(o => o.Validate()).Should()
            .Throw<InvalidOperationException>()
            .WithMessage("RabbitMQ:ManagementUrl*plain http*non-loopback*https*");

    [Theory]
    [InlineData("")]
    [InlineData("rabbit:15672")]
    [InlineData("/api")]
    [InlineData("ftp://localhost")]
    public void Validate_ManagementUrl_NotAnHttpUrl_Throws(string url) =>
        new FailedMessagesOptions { Enabled = true, ManagementUrl = url }.Invoking(o => o.Validate()).Should()
            .Throw<InvalidOperationException>().WithMessage("RabbitMQ:ManagementUrl must be an absolute http(s) URL*");

    // The URL is read from RabbitMQ:ManagementUrl (same section as Host/Username/Password); absent or blank
    // falls back to the localhost default.
    [Theory]
    [InlineData("https://rabbit.example:15671", "https://rabbit.example:15671")]
    [InlineData(null, FailedMessagesOptions.DefaultManagementUrl)]
    [InlineData("", FailedMessagesOptions.DefaultManagementUrl)]
    [InlineData("  ", FailedMessagesOptions.DefaultManagementUrl)]
    public void IntegrationModule_ReadsManagementUrlFromRabbitMqSection(string? rabbitMq, string expected) =>
        ResolveOptions(enabled: true, rabbitMq, legacy: null).ManagementUrl.Should().Be(expected);

    [Fact]
    public void IntegrationModule_EnabledWithPlainHttpRemoteManagementUrl_FailsOnResolve() =>
        FluentActions.Invoking(() => ResolveOptions(enabled: true, "http://rabbit.internal.example:15672", legacy: null))
            .Should().Throw<InvalidOperationException>().WithMessage("RabbitMQ:ManagementUrl*plain http*non-loopback*");

    // No fallback to the old key, and it must not be silently ignored either.
    [Theory]
    [InlineData(null)]
    [InlineData("https://new.example:15671")]
    public void IntegrationModule_EnabledWithLegacyKey_FailsOnResolve(string? rabbitMq) =>
        FluentActions.Invoking(() => ResolveOptions(enabled: true, rabbitMq, legacy: "https://old.example:15671"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("FailedMessages:ManagementUrl has moved to RabbitMQ:ManagementUrl*");

    // Validate() is a no-op while disabled, so a hand-edited config must not block startup.
    [Fact]
    public void IntegrationModule_DisabledWithLegacyKey_DoesNotThrow() =>
        ResolveOptions(enabled: false, null, legacy: "https://old.example:15671").ManagementUrl
            .Should().Be(FailedMessagesOptions.DefaultManagementUrl);

    private static FailedMessagesOptions ResolveOptions(
        bool enabled, string? rabbitMq, string? legacy, string? skippedRetention = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FailedMessages:Enabled"] = enabled.ToString(),
                ["RabbitMQ:ManagementUrl"] = rabbitMq,
                ["FailedMessages:ManagementUrl"] = legacy,
                ["FailedMessages:SkippedPendingRetentionDays"] = skippedRetention,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddIntegrationModule(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<FailedMessagesOptions>>().Value;
    }
}
