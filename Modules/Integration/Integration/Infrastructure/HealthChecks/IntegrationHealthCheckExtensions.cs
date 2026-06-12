using Integration.Contracts.FileSource;
using Integration.Infrastructure.FileSink;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Integration.Infrastructure.HealthChecks;

public static class IntegrationHealthCheckExtensions
{
    /// <summary>
    /// Registers the Integration module's external-dependency health checks: the inbound and
    /// outbound AS400 SFTP endpoints. Each side is probed only when its <c>FileSource</c> is
    /// <c>Sftp</c>; in <c>Local</c> mode the check reports disabled. Tagged <c>external</c> —
    /// included in <c>/health</c> and <c>/health/external</c>, excluded from <c>/health/ready</c>
    /// so an SFTP outage never removes a node from load balancing.
    /// <para>The <c>Configure&lt;InboundFileSourceOptions&gt;</c> / <c>Configure&lt;OutboundFileSinkOptions&gt;</c>
    /// bindings are already done in <c>IntegrationModule</c>.</para>
    /// </summary>
    public static IHealthChecksBuilder AddIntegrationHealthChecks(this IHealthChecksBuilder builder)
    {
        builder.Add(new HealthCheckRegistration(
            "sftp-inbound",
            sp =>
            {
                var o = sp.GetRequiredService<IOptions<InboundFileSourceOptions>>().Value;
                var enabled = FileTransferTransport.IsSftp(o.FileSource);
                return new SftpHealthCheck("inbound", enabled, o.Sftp.Host, o.Sftp.Port, o.Sftp.Username, o.Sftp.Password);
            },
            failureStatus: null, // SftpHealthCheck catches and returns Unhealthy itself; never throws here.
            tags: ["external", "sftp"]));

        builder.Add(new HealthCheckRegistration(
            "sftp-outbound",
            sp =>
            {
                var o = sp.GetRequiredService<IOptions<OutboundFileSinkOptions>>().Value;
                var enabled = FileTransferTransport.IsSftp(o.FileSource);
                return new SftpHealthCheck("outbound", enabled, o.Sftp.Host, o.Sftp.Port, o.Sftp.Username, o.Sftp.Password);
            },
            failureStatus: null, // SftpHealthCheck catches and returns Unhealthy itself; never throws here.
            tags: ["external", "sftp"]));

        return builder;
    }
}
