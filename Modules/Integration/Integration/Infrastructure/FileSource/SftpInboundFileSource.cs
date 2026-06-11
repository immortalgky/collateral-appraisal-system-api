using Integration.Contracts.FileSource;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Integration.Infrastructure.FileSource;

/// <summary>
/// SFTP inbound file source — reads files from an SFTP server.
/// Intended for UAT and production (FileTransfer:Inbound:FileSource = Sftp).
/// Directory, pattern, and processed directory are passed by the caller (resolved from FileInterfaceConfigs).
/// </summary>
public class SftpInboundFileSource(
    IOptions<InboundFileSourceOptions> options,
    ILogger<SftpInboundFileSource> logger) : IInboundFileSource
{
    private readonly InboundSftpOptions _opts = options.Value.Sftp;

    public async Task<IReadOnlyList<InboundFileInfo>> ListFilesAsync(string directory, string filePattern, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                var files = client
                    .ListDirectory(directory)
                    .Where(f => !f.IsDirectory && MatchesPattern(f.Name, filePattern))
                    .OrderBy(f => f.Name)
                    .Select(f => new InboundFileInfo(f.Name, f.FullName))
                    .ToList();

                logger.LogInformation("[InboundFileSource:Sftp] Found {Count} file(s) in {Dir}",
                    files.Count, directory);
                return (IReadOnlyList<InboundFileInfo>)files;
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(InboundFileInfo file, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();

        await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                client.DownloadFile(file.FullPath, ms);
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);

        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public async Task ArchiveAsync(InboundFileInfo file, string processedDirectory, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                var destination = $"{processedDirectory.TrimEnd('/')}/{file.FileName}";
                client.RenameFile(file.FullPath, destination);
                logger.LogInformation("[InboundFileSource:Sftp] Archived {File} → {Dest}",
                    file.FileName, destination);
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    private SftpClient CreateClient()
    {
        var connectionInfo = new ConnectionInfo(
            _opts.Host,
            _opts.Port,
            _opts.Username,
            new PasswordAuthenticationMethod(_opts.Username, _opts.Password));

        return new SftpClient(connectionInfo);
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern.StartsWith('*') && pattern.EndsWith('*'))
        {
            var middle = pattern.Trim('*');
            return fileName.Contains(middle, StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.StartsWith('*'))
            return fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        if (pattern.EndsWith('*'))
            return fileName.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*');
            return fileName.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(parts[^1], StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
