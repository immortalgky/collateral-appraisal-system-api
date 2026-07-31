using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Configuration;
using Shared.Security;

namespace Shared.Tests.Configuration;

public class PlaintextSecretAuditTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(values);
        return builder.Build();
    }

    private static (string message, LogLevel level)? RunAudit(Dictionary<string, string?> values)
    {
        var logger = new CapturingLogger<PlaintextSecretAudit>();
        var audit = new PlaintextSecretAudit(BuildConfig(values), logger);
        audit.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return logger.Entries.Count == 0 ? null : logger.Entries[0];
    }

    [Fact]
    public void Flags_a_plaintext_secret_by_key_without_logging_the_value()
    {
        const string secret = "plaintext-password-fixture";
        var entry = RunAudit(new Dictionary<string, string?> { ["Mail:Password"] = secret });

        entry.Should().NotBeNull();
        entry!.Value.level.Should().Be(LogLevel.Error);
        entry.Value.message.Should().Contain("Mail:Password").And.NotContain(secret);
    }

    [Fact]
    public void Does_not_flag_an_encrypted_secret()
    {
        using var rsa = RSA.Create(2048);
        var cert = new CertificateRequest("CN=t", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var entry = RunAudit(new Dictionary<string, string?>
        {
            ["Mail:Password"] = SecretProtector.Protect("pw", cert),
        });

        entry.Should().BeNull();
    }

    [Fact]
    public void Does_not_flag_an_integrated_security_connection_string()
    {
        var entry = RunAudit(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Server=sql01;Database=CAS;Integrated Security=True;Encrypt=True;",
        });

        entry.Should().BeNull();
    }

    [Fact]
    public void Flags_a_connection_string_that_carries_a_password()
    {
        var entry = RunAudit(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Server=sql01;User Id=app;Password=S3cr3t!;",
        });

        entry.Should().NotBeNull();
        entry!.Value.message.Should().Contain("ConnectionStrings:Database").And.NotContain("S3cr3t!");
    }

    [Fact]
    public void Stays_silent_when_everything_is_clean()
    {
        var entry = RunAudit(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Mail:Password"] = "",
        });

        entry.Should().BeNull();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(string message, LogLevel level)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((formatter(state, exception), logLevel));
    }
}
