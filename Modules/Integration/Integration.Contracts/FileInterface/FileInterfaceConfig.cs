namespace Integration.Contracts.FileInterface;

/// <summary>
/// DB-sourced configuration for a single file interface, keyed by <see cref="InterfaceCode"/>.
/// Produced by <see cref="IFileInterfaceConfigProvider"/>.
/// </summary>
public sealed record FileInterfaceConfig(
    string InterfaceCode,
    string Direction,
    string? FileNamePrefix,
    string? FileNameDateFormat,
    string? FileExtension,
    string? Directory,
    string? ProcessedDirectory,
    string? FilePattern,
    bool IsActive
);
