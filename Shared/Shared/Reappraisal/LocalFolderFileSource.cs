using Microsoft.Extensions.Options;

namespace Shared.Reappraisal;

/// <summary>
/// Local-folder file source — reads COLLATREV files from a directory on the local filesystem.
/// Intended for development and integration testing (FileSource = Local).
/// </summary>
public class LocalFolderFileSource(IOptions<ReappraisalOptions> options) : IReappraisalFileSource
{
    private readonly LocalFolderOptions _opts = options.Value.Local;

    public Task<IReadOnlyList<ReappraisalFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        var dir = Path.GetFullPath(_opts.Path);
        if (!Directory.Exists(dir))
        {
            // Return empty list when the inbox does not exist yet — job is a no-op.
            return Task.FromResult<IReadOnlyList<ReappraisalFileInfo>>(Array.Empty<ReappraisalFileInfo>());
        }

        var files = Directory
            .GetFiles(dir, _opts.FilePattern, SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .Select(fullPath => new ReappraisalFileInfo(Path.GetFileName(fullPath), fullPath))
            .ToList();

        return Task.FromResult<IReadOnlyList<ReappraisalFileInfo>>(files);
    }

    public Task<Stream> OpenReadAsync(ReappraisalFileInfo file, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(file.FullPath);
        return Task.FromResult(stream);
    }

    public Task ArchiveAsync(ReappraisalFileInfo file, CancellationToken cancellationToken = default)
    {
        var processedDir = Path.GetFullPath(_opts.ProcessedPath);
        Directory.CreateDirectory(processedDir);

        var destination = Path.Combine(processedDir, file.FileName);
        // Overwrite if a same-named file is already archived (re-run scenario).
        if (File.Exists(destination))
            File.Delete(destination);

        File.Move(file.FullPath, destination);
        return Task.CompletedTask;
    }
}
