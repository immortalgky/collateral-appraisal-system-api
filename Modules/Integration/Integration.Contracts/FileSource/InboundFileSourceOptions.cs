namespace Integration.Contracts.FileSource;

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
