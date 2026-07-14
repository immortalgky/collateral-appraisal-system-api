using System.Text;
using Integration.Contracts.FileSink;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Integration.Infrastructure.FileSink;

/// <summary>
/// Writes outbound interface files to a local directory. Intended for dev / integration testing
/// (FileTransfer:Outbound:FileSource = Local). UTF-8 without BOM.
/// The directory is supplied by the caller (resolved from FileInterfaceConfigs).
/// </summary>
public class LocalFileSink(IHostEnvironment environment, ILogger<LocalFileSink> logger) : IOutboundFileSink
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default)
    {
        // Resolve a relative directory against the app content root, NOT the process CWD — on IIS the
        // worker-process CWD is System32\inetsrv, which would silently misplace the file.
        var rooted = Path.IsPathRooted(directory) ? directory : Path.Combine(environment.ContentRootPath, directory);
        var dir = Path.GetFullPath(rooted);
        Directory.CreateDirectory(dir);

        // Defense-in-depth: never let a file name escape the configured directory.
        var fullPath = Path.Combine(dir, Path.GetFileName(fileName));
        await File.WriteAllTextAsync(fullPath, content, Utf8NoBom, cancellationToken);

        logger.LogInformation("[OutboundFileSink:Local] Wrote {File} ({Chars} chars) to {Dir}",
            fileName, content.Length, dir);
    }

    public async Task WriteAsync(string directory, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var rooted = Path.IsPathRooted(directory) ? directory : Path.Combine(environment.ContentRootPath, directory);
        var dir = Path.GetFullPath(rooted);
        Directory.CreateDirectory(dir);

        var fullPath = Path.Combine(dir, Path.GetFileName(fileName));
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        logger.LogInformation("[OutboundFileSink:Local] Wrote {File} ({Bytes} bytes) to {Dir}",
            fileName, content.Length, dir);
    }
}
