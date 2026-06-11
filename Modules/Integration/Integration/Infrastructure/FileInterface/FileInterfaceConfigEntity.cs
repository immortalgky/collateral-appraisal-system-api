namespace Integration.Infrastructure.FileInterface;

/// <summary>
/// EF entity for <c>integration.FileInterfaceConfigs</c> — one row per file interface.
/// Keyed by a unique <see cref="InterfaceCode"/>. Non-secret naming/path config that operators
/// can update in the DB without a redeploy.
/// </summary>
public class FileInterfaceConfigEntity
{
    public Guid Id { get; private set; }

    /// <summary>Unique interface identifier (e.g. "REGULATORY", "COLLATERAL_RESULT", "REAPPRAISAL").</summary>
    public string InterfaceCode { get; private set; } = null!;

    /// <summary>"In" or "Out".</summary>
    public string Direction { get; private set; } = null!;

    public string? FileNamePrefix { get; private set; }
    public string? FileNameDateFormat { get; private set; }
    public string? FileExtension { get; private set; }

    /// <summary>Outbound: destination directory. Inbound: inbox directory.</summary>
    public string? Directory { get; private set; }

    /// <summary>Inbound only: directory to move processed files into.</summary>
    public string? ProcessedDirectory { get; private set; }

    /// <summary>Inbound only: glob pattern to match inbound files.</summary>
    public string? FilePattern { get; private set; }

    public bool IsActive { get; private set; }

    // No audit columns: global conventions only set CreatedBy/UpdatedBy max-length and add none here.

    private FileInterfaceConfigEntity() { }
}
