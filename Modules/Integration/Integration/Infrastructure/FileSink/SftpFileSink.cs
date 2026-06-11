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

    public async Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();
            try
            {
                var bytes = Utf8NoBom.GetBytes(content);
                using var ms = new MemoryStream(bytes);

                var remotePath = $"{directory.TrimEnd('/')}/{Path.GetFileName(fileName)}";
                client.UploadFile(ms, remotePath, canOverride: true);

                logger.LogInformation("[OutboundFileSink:Sftp] Uploaded {File} ({Chars} chars) → {Path}",
                    fileName, content.Length, remotePath);
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
}
