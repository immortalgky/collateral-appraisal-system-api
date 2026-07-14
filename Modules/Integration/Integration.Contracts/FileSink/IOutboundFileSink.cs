namespace Integration.Contracts.FileSink;

/// <summary>
/// Port for writing an outbound interface file to its configured destination
/// (local folder in dev, SFTP in UAT/prod). The caller supplies the directory, file name, and
/// content; the adapter owns the transport (config-switched in the Integration module).
///
/// Lives in Integration.Contracts so producing modules (e.g. Collateral) depend on the contract
/// only — not on the SFTP/transport implementation in the Integration module.
/// </summary>
public interface IOutboundFileSink
{
    /// <summary>
    /// Writes <paramref name="content"/> to a file named <paramref name="fileName"/> in
    /// <paramref name="directory"/>, overwriting any existing file with the same name.
    /// </summary>
    Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes binary <paramref name="content"/> (e.g. an xlsx workbook) to a file named
    /// <paramref name="fileName"/> in <paramref name="directory"/>, overwriting any existing file
    /// with the same name.
    /// </summary>
    Task WriteAsync(string directory, string fileName, byte[] content, CancellationToken cancellationToken = default);
}
