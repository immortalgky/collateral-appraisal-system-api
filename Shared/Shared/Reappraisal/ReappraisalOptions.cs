namespace Shared.Reappraisal;

/// <summary>
/// Root configuration section bound from <c>Reappraisal</c> in appsettings.
/// </summary>
public class ReappraisalOptions
{
    public const string SectionName = "Reappraisal";

    /// <summary>
    /// Selects the transport provider.
    /// Valid values: <c>Local</c> (default, for dev) | <c>Sftp</c> (for UAT/prod).
    /// </summary>
    public string FileSource { get; set; } = "Local";

    public LocalFolderOptions Local { get; set; } = new();
    public SftpOptions Sftp { get; set; } = new();
}

/// <summary>
/// Configuration for the local-folder file source (dev / integration testing).
/// </summary>
public class LocalFolderOptions
{
    /// <summary>
    /// Directory to scan for COLLATREV files.
    /// Default: <c>./reappraisal/inbox</c> (relative to working dir).
    /// </summary>
    public string Path { get; set; } = "./reappraisal/inbox";

    /// <summary>
    /// Directory to move processed files into.
    /// Default: <c>./reappraisal/processed</c>.
    /// </summary>
    public string ProcessedPath { get; set; } = "./reappraisal/processed";

    /// <summary>Glob pattern for COLLATREV filenames.</summary>
    public string FilePattern { get; set; } = "AS400_COLLATREV_*.txt";
}

/// <summary>
/// Configuration for the SFTP file source (UAT / prod).
/// IMPORTANT: Host/Username/Password must come from user-secrets or environment variables —
/// never commit credentials to appsettings.json.
/// </summary>
public class SftpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;

    // TODO(confirm): SFTP credentials belong in user-secrets / key vault, not appsettings.json.
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Remote directory containing inbound COLLATREV files.</summary>
    public string RemoteDirectory { get; set; } = "/incoming/collatrev";

    /// <summary>Glob pattern for COLLATREV filenames on the SFTP server.</summary>
    public string FilePattern { get; set; } = "AS400_COLLATREV_*.txt";

    /// <summary>Remote directory to move processed files into after ingestion.</summary>
    public string ProcessedDirectory { get; set; } = "/processed/collatrev";
}
