using System.Text;
using Integration.Contracts.FileSink;
using Microsoft.Extensions.Logging;

namespace Integration.Infrastructure.FileSink;

/// <summary>
/// Writes outbound interface files to a local directory. Intended for dev / integration testing
/// (FileTransfer:Outbound:FileSource = Local). UTF-8 without BOM.
/// The directory is supplied by the caller (resolved from FileInterfaceConfigs).
/// </summary>
public class LocalFileSink(ILogger<LocalFileSink> logger) : IOutboundFileSink
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task WriteAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetFullPath(directory);
        Directory.CreateDirectory(dir);

        // Defense-in-depth: never let a file name escape the configured directory.
        var fullPath = Path.Combine(dir, Path.GetFileName(fileName));
        await File.WriteAllTextAsync(fullPath, content, Utf8NoBom, cancellationToken);

        logger.LogInformation("[OutboundFileSink:Local] Wrote {File} ({Chars} chars) to {Dir}",
            fileName, content.Length, dir);
    }
}
