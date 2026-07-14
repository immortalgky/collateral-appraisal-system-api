using System.Text;
using Integration.Contracts.FileSink;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Integration.Infrastructure.FileSink;

/// <summary>
/// Uploads outbound interface files to an SFTP server. Intended for UAT / production
/// (FileTransfer:Outbound:FileSource = Sftp). Mirrors the inbound SftpFileSource connection pattern.
/// The directory is supplied by the caller (resolved from FileInterfaceConfigs).
/// UTF-8 without BOM.
/// </summary>
public class SftpFileSink(
    IOptions<OutboundFileSinkOptions> options,
    ILogger<SftpFileSink> logger) : IOutboundFileSink
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly SftpSinkOptions _opts = options.Value.Sftp;

    public Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default) =>
        UploadAsync(directory, fileName, Utf8NoBom.GetBytes(content), content.Length + " chars", cancellationToken);

    public Task WriteAsync(string directory, string fileName, byte[] content, CancellationToken cancellationToken = default) =>
        UploadAsync(directory, fileName, content, content.Length + " bytes", cancellationToken);

    private async Task UploadAsync(string directory, string fileName, byte[] bytes, string sizeDescription, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                using var ms = new MemoryStream(bytes);

                EnsureRemoteDirectory(client, directory);

                var remotePath = $"{directory.TrimEnd('/')}/{Path.GetFileName(fileName)}";
                client.UploadFile(ms, remotePath, canOverride: true);

                logger.LogInformation("[OutboundFileSink:Sftp] Uploaded {File} ({Size}) → {Path}",
                    fileName, sizeDescription, remotePath);
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

    /// <summary>Creates the remote directory path (recursively) if it does not already exist.</summary>
    private static void EnsureRemoteDirectory(SftpClient client, string directory)
    {
        var dir = directory.TrimEnd('/');
        if (string.IsNullOrEmpty(dir) || client.Exists(dir))
            return;

        var path = dir.StartsWith('/') ? string.Empty : ".";
        foreach (var segment in dir.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            path = $"{path}/{segment}";
            if (!client.Exists(path))
                client.CreateDirectory(path);
        }
    }
}
