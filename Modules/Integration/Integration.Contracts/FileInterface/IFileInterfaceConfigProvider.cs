namespace Integration.Contracts.FileInterface;

/// <summary>
/// Provides <see cref="FileInterfaceConfig"/> rows from the <c>integration.FileInterfaceConfigs</c>
/// table, with a short in-memory TTL cache so jobs pick up changes without a redeploy.
/// </summary>
public interface IFileInterfaceConfigProvider
{
    /// <summary>
    /// Returns the config row for <paramref name="interfaceCode"/>, or <c>null</c> when the row
    /// does not exist in the database.
    /// </summary>
    Task<FileInterfaceConfig?> GetAsync(string interfaceCode, CancellationToken cancellationToken = default);
}
