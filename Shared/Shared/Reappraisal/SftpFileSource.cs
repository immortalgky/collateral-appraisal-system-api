using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Shared.Reappraisal;

/// <summary>
/// SFTP file source — reads COLLATREV files from an SFTP server.
/// Intended for UAT and production (FileSource = Sftp).
///
/// SFTP credentials (Host/Username/Password) must be configured via
/// user-secrets or environment variables — never commit them to appsettings.json.
/// </summary>
public class SftpFileSource(
    IOptions<ReappraisalOptions> options,
    ILogger<SftpFileSource> logger) : IReappraisalFileSource
{
    private readonly SftpOptions _opts = options.Value.Sftp;

    public async Task<IReadOnlyList<ReappraisalFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                var files = client
                    .ListDirectory(_opts.RemoteDirectory)
                    .Where(f => !f.IsDirectory && MatchesPattern(f.Name, _opts.FilePattern))
                    .OrderBy(f => f.Name)
                    .Select(f => new ReappraisalFileInfo(f.Name, f.FullName))
                    .ToList();

                logger.LogInformation("[SFTP] Found {Count} COLLATREV file(s) in {Dir}", files.Count, _opts.RemoteDirectory);
                return (IReadOnlyList<ReappraisalFileInfo>)files;
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(ReappraisalFileInfo file, CancellationToken cancellationToken = default)
    {
        // Download the remote file into a MemoryStream so we can close the SFTP session
        // before the caller processes the content.
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

    public async Task ArchiveAsync(ReappraisalFileInfo file, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                var destination = $"{_opts.ProcessedDirectory}/{file.FileName}";
                client.RenameFile(file.FullPath, destination);
                logger.LogInformation("[SFTP] Archived {File} → {Dest}", file.FileName, destination);
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

    /// <summary>
    /// Very simple glob-style pattern match supporting only leading/trailing '*' wildcards
    /// (e.g. <c>AS400_COLLATREV_*.txt</c>).
    /// </summary>
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
