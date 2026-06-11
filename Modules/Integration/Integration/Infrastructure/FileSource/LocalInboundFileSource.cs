using Integration.Contracts.FileSource;
using Microsoft.Extensions.Hosting;

namespace Integration.Infrastructure.FileSource;

/// <summary>
/// Local-folder inbound file source — reads files from a directory on the local filesystem.
/// Intended for development and integration testing (FileTransfer:Inbound:FileSource = Local).
/// Directory and file pattern are passed by the caller (resolved from FileInterfaceConfigs).
/// </summary>
public class LocalInboundFileSource(IHostEnvironment environment) : IInboundFileSource
{
    // Resolve a relative directory against the app content root, NOT the process CWD — on IIS the
    // worker-process CWD is System32\inetsrv, which would silently look in / write to the wrong place.
    private string Resolve(string directory) =>
        Path.GetFullPath(Path.IsPathRooted(directory) ? directory : Path.Combine(environment.ContentRootPath, directory));

    public Task<IReadOnlyList<InboundFileInfo>> ListFilesAsync(string directory, string filePattern, CancellationToken cancellationToken = default)
    {
        var dir = Resolve(directory);
        if (!Directory.Exists(dir))
        {
            // Return empty list when the inbox does not exist yet — job is a no-op.
            return Task.FromResult<IReadOnlyList<InboundFileInfo>>(Array.Empty<InboundFileInfo>());
        }

        var files = Directory
            .GetFiles(dir, filePattern, SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .Select(fullPath => new InboundFileInfo(Path.GetFileName(fullPath), fullPath))
            .ToList();

        return Task.FromResult<IReadOnlyList<InboundFileInfo>>(files);
    }

    public Task<Stream> OpenReadAsync(InboundFileInfo file, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(file.FullPath);
        return Task.FromResult(stream);
    }

    public Task ArchiveAsync(InboundFileInfo file, string processedDirectory, CancellationToken cancellationToken = default)
    {
        var processedDir = Resolve(processedDirectory);
        Directory.CreateDirectory(processedDir);

        var destination = Path.Combine(processedDir, file.FileName);
        if (File.Exists(destination))
            File.Delete(destination);

        File.Move(file.FullPath, destination);
        return Task.CompletedTask;
    }
}
