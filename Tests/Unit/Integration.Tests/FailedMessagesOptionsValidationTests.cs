using FluentAssertions;
using Integration.FailedMessages;

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
            .WithMessage("FailedMessages:ManagementUrl*plain http*non-loopback*https*");

    [Theory]
    [InlineData("")]
    [InlineData("rabbit:15672")]
    [InlineData("/api")]
    [InlineData("ftp://localhost")]
    public void Validate_ManagementUrl_NotAnHttpUrl_Throws(string url) =>
        new FailedMessagesOptions { Enabled = true, ManagementUrl = url }.Invoking(o => o.Validate()).Should()
            .Throw<InvalidOperationException>().WithMessage("FailedMessages:ManagementUrl must be an absolute http(s) URL*");
}
