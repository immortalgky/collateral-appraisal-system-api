namespace Integration.Infrastructure.FileSink;

/// <summary>
/// Configuration for the outbound file sink transport, bound from <c>FileTransfer:Outbound</c>.
/// Non-secret naming/paths now come from <c>integration.FileInterfaceConfigs</c>.
/// </summary>
public class OutboundFileSinkOptions
{
    public const string SectionName = "FileTransfer:Outbound";

    /// <summary>Transport selector: <c>Local</c> (dev, default) | <c>Sftp</c> (UAT/prod).</summary>
    public string FileSource { get; set; } = "Local";

    public SftpSinkOptions Sftp { get; set; } = new();
}

/// <summary>
/// SFTP destination credentials (UAT / prod). Host/Username/Password come from appsettings per
/// project convention.
/// </summary>
public class SftpSinkOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
