namespace Integration.Contracts.FileSource;

/// <summary>
/// Single source of truth for the file-transfer transport selector, so the DI wiring
/// (<c>IntegrationModule</c>) and the SFTP health checks can't drift on how "Sftp" is matched.
/// </summary>
public static class FileTransferTransport
{
    public const string Sftp = "Sftp";

    /// <summary>True when the configured <c>FileSource</c> selects the SFTP transport (else Local).</summary>
    public static bool IsSftp(string? fileSource) =>
        string.Equals(fileSource, Sftp, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Configuration for the inbound file source transport, bound from <c>FileTransfer:Inbound</c>.
/// Non-secret naming/path/pattern now come from <c>integration.FileInterfaceConfigs</c>.
/// </summary>
public class InboundFileSourceOptions
{
    public const string SectionName = "FileTransfer:Inbound";

    /// <summary>
    /// Selects the transport provider.
    /// Valid values: <c>Local</c> (default, for dev) | <c>Sftp</c> (for UAT/prod).
    /// </summary>
    public string FileSource { get; set; } = "Local";

    public InboundSftpOptions Sftp { get; set; } = new();
}

/// <summary>
/// SFTP credentials for the inbound file transport (UAT / prod).
/// Host/Username/Password come from appsettings per project convention.
/// </summary>
public class InboundSftpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
