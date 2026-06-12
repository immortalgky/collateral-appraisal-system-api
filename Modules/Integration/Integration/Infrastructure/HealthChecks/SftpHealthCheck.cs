using Microsoft.Extensions.Diagnostics.HealthChecks;
using Renci.SshNet;

namespace Integration.Infrastructure.HealthChecks;

/// <summary>
/// Probes connectivity to an AS400 file-transfer SFTP endpoint (inbound or outbound) by opening a
/// connection and disconnecting — no file is transferred. Mirrors the connection pattern in
/// <see cref="FileSink.SftpFileSink"/>. When the transport is not SFTP (FileSource=Local) the check
/// reports Healthy with <c>state=disabled</c>. One instance is registered per side; both are tagged
/// <c>external</c> so they surface in <c>/health</c> and <c>/health/external</c> but never affect the
/// readiness probe. The explicit timeout caps a hung connection so the health endpoint can't stall.
/// </summary>
internal sealed class SftpHealthCheck(
    string label,
    bool enabled,
    string host,
    int port,
    string username,
    string password) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!enabled)
            return HealthCheckResult.Healthy(
                $"SFTP {label} disabled (FileSource != Sftp).",
                new Dictionary<string, object> { ["state"] = "disabled" });

        try
        {
            await Task.Run(() =>
            {
                var connectionInfo = new ConnectionInfo(
                    host, port, username,
                    new PasswordAuthenticationMethod(username, password))
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                using var client = new SftpClient(connectionInfo);
                client.Connect();
                var connected = client.IsConnected;
                client.Disconnect();

                if (!connected)
                    throw new InvalidOperationException("SFTP client did not report a connected state.");
            }, cancellationToken);

            return HealthCheckResult.Healthy($"Connected to SFTP {label} {host}:{port}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"SFTP {label} connect to {host}:{port} failed: {ex.Message}", ex);
        }
    }
}
