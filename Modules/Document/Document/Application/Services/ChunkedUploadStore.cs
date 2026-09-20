using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Document.Services;

internal sealed class ChunkedUploadStore(
    IWebHostEnvironment webHostEnvironment,
    IOptions<FileStorageConfiguration> fileStorageOptions,
    ILogger<ChunkedUploadStore> logger) : IChunkedUploadStore
{
    // ".part", not ".json": this folder sits under the tree Program.cs serves statically with
    // no authorization, and an unmapped extension is not served (ServeUnknownFileTypes is off).
    // Named .json, this file handed the owner's id, the file name and the session id to anyone
    // who knew the upload id.
    private const string MetaFileName = "meta.part";
    private const string DataFileName = "data.part";
    private const string ChunkedFolderName = "chunked";
    private const int CopyBufferSize = 1024 * 1024;

    private readonly FileStorageConfiguration _configuration = fileStorageOptions.Value;

    public async Task CreateAsync(ChunkedUploadMeta meta, CancellationToken cancellationToken = default)
    {
        var directory = DirectoryFor(meta.UploadId);
        Directory.CreateDirectory(directory);

        // CreateNew on both: the id is a fresh GUIDv7, so anything already here means two uploads
        // were handed the same id, and continuing would mix their bytes together.
        await using (var metaFile = new FileStream(
                         Path.Combine(directory, MetaFileName),
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None))
        {
            await JsonSerializer.SerializeAsync(metaFile, meta, cancellationToken: cancellationToken);
        }

        await using var dataFile = new FileStream(
            Path.Combine(directory, DataFileName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
    }

    public async Task<ChunkedUploadMeta?> TryReadMetaAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var metaFile = new FileStream(
                Path.Combine(DirectoryFor(uploadId), MetaFileName),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            return await JsonSerializer.DeserializeAsync<ChunkedUploadMeta>(
                metaFile,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Opening and catching, rather than asking File.Exists first: over SMB the answer to
            // that question is cached for seconds at a time, so the other node can be told a
            // folder is still there after this one has swept it away.
            return null;
        }
        catch (IOException)
        {
            // Held exclusively by a completion in progress.
            throw new ConflictException("This upload is being completed. Retry shortly.");
        }
    }

    public async Task<ChunkedUploadClaim?> TryClaimAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        FileStream metaFile;

        try
        {
            metaFile = new FileStream(
                Path.Combine(DirectoryFor(uploadId), MetaFileName),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            // Someone else is completing this upload. Held, not failed.
            throw new ConflictException("This upload is being completed. Retry shortly.");
        }

        try
        {
            var meta = await JsonSerializer.DeserializeAsync<ChunkedUploadMeta>(
                metaFile, cancellationToken: cancellationToken);

            if (meta is null)
            {
                await metaFile.DisposeAsync();
                return null;
            }

            return new ChunkedUploadClaim(meta, metaFile);
        }
        catch
        {
            await metaFile.DisposeAsync();
            throw;
        }
    }

    public async Task<ChunkAppendResult> AppendAsync(
        Guid uploadId,
        long offset,
        Stream body,
        long declaredFileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        FileStream data;

        try
        {
            // FileShare.None is the lock. SMB enforces share modes on the file server, so this
            // holds across both app nodes without anything else keeping track of who is writing.
            data = new FileStream(
                Path.Combine(DirectoryFor(uploadId), DataFileName),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ChunkAppendResult(ChunkAppendStatus.NotFound, 0);
        }
        catch (IOException)
        {
            // The other writer holds it. Everything else that can go wrong here throws something
            // more specific, so a plain IOException on open is a sharing violation.
            return new ChunkAppendResult(ChunkAppendStatus.Busy, 0);
        }

        await using (data)
        {
            // The length of the open handle, never FileInfo.Length: the cached directory entry
            // this machine holds for a share can be seconds out of date, and answering from it
            // would tell a client to resend bytes that are already here.
            var received = data.Length;

            if (offset != received)
                return new ChunkAppendResult(ChunkAppendStatus.OffsetMismatch, received);

            if (received >= declaredFileSizeBytes)
                return new ChunkAppendResult(ChunkAppendStatus.TooLarge, received);

            data.Seek(0, SeekOrigin.End);

            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            try
            {
                int read;
                while ((read = await body.ReadAsync(buffer.AsMemory(0, CopyBufferSize), cancellationToken)) > 0)
                {
                    if (received + read > declaredFileSizeBytes)
                    {
                        // Stop at the declared size rather than writing past it. The bytes already
                        // written stay, so the client can be told the truth about where the file
                        // now ends and send the rest of it correctly.
                        await data.FlushAsync(cancellationToken);
                        return new ChunkAppendResult(ChunkAppendStatus.TooLarge, data.Length);
                    }

                    await data.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                }

                await data.FlushAsync(cancellationToken);
                return new ChunkAppendResult(ChunkAppendStatus.Ok, received);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public Task<long?> GetReceivedBytesAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Opened, not stat'ed: FileInfo.Length answers from a cached directory entry that a
            // share can hold for seconds, and the whole point of this number is that it is current.
            using var data = new FileStream(
                DataPath(uploadId), FileMode.Open, FileAccess.Read, FileShare.Read);

            return Task.FromResult<long?>(data.Length);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<long?>(null);
        }
        catch (IOException)
        {
            // A chunk is being written right now — the client fired completion while its last
            // request was still in flight. That is a retry, not a failure.
            throw new ConflictException("Another chunk of this upload is being written. Retry shortly.");
        }
    }

    public string DataPath(Guid uploadId) => Path.Combine(DirectoryFor(uploadId), DataFileName);

    public void Delete(Guid uploadId) => TryDeleteDirectory(DirectoryFor(uploadId));

    public int DeleteExpired(TimeSpan olderThan)
    {
        var root = ChunkedRoot();
        var cutoff = DateTime.UtcNow - olderThan;
        var removed = 0;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing has been uploaded in pieces yet.
            return 0;
        }

        foreach (var directory in directories)
        {
            DateTime lastWrite;
            try
            {
                lastWrite = File.GetLastWriteTimeUtc(Path.Combine(directory, DataFileName));
            }
            catch (IOException)
            {
                continue;
            }

            // A folder whose data file cannot be read at all comes back as the epoch, which is
            // older than any cutoff — exactly what should be swept.
            if (lastWrite > cutoff) continue;

            if (TryDeleteDirectory(directory)) removed++;
        }

        if (removed > 0)
            logger.LogInformation("Removed {Count} abandoned chunked upload folders", removed);

        return removed;
    }

    private bool TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            // The other node swept it first. Both run this job; neither owns it.
            return false;
        }
        catch (IOException ex)
        {
            // An upload still being written to holds its file open. It will be swept next time.
            logger.LogDebug(ex, "Could not remove chunked upload folder {Directory}", directory);
            return false;
        }
    }

    private string ChunkedRoot() => Path.Combine(
        DocumentStorage.BasePath(_configuration, webHostEnvironment),
        _configuration.RootPath.TrimStart('/'),
        _configuration.TempPath,
        ChunkedFolderName);

    private string DirectoryFor(Guid uploadId) => Path.Combine(ChunkedRoot(), uploadId.ToString("N"));
}
