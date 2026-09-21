using System.Buffers;
using Document.Domain.Documents;
using Document.Domain.Documents.Features.UploadDocument;
using Document.Domain.Documents.Models;
using Document.Domain.UploadSessions.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Shared.Configurations;
using Shared.Data;
using Shared.Identity;
using Shared.Time;

namespace Document.Services;

public class DocumentService(
    ICurrentUserService currentUserService,
    IDocumentUnitOfWork uow,
    IWebHostEnvironment webHostEnvironment,
    IOptions<FileStorageConfiguration> fileStorageOptions,
    IImageResizeService imageResizeService,
    IChunkedUploadStore chunkedUploadStore,
    ILogger<DocumentService> logger,
    IDateTimeProvider dateTimeProvider)
    : IDocumentService, IDocumentCreatorService
{
    private readonly FileStorageConfiguration _fileStorageConfiguration = fileStorageOptions.Value;

    private readonly IRepository<Domain.Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Domain.Documents.Models.Document, Guid>();

    private readonly IRepository<UploadSession, Guid> _uploadSessionRepository = uow.Repository<UploadSession, Guid>();

    public async Task<UploadDocumentResult> UploadAsync(IFormFile file, Guid uploadSessionId, string documentType,
        string documentCategory, string? description, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Uploading file {FileName} ({FileSize} bytes) to session {SessionId}",
            file.FileName,
            file.Length,
            uploadSessionId);

        // Look the session up before a byte is written. Its id comes from the client, so it is the
        // likeliest thing here to be wrong, and finding out afterwards leaves a file on the share
        // that no row points at — in a folder nothing sweeps.
        var uploadSession = await _uploadSessionRepository.GetByIdAsync(uploadSessionId, cancellationToken);
        if (uploadSession is null)
        {
            logger.LogError("Upload session {SessionId} not found", uploadSessionId);
            throw new NotFoundException($"Upload session {uploadSessionId} not found");
        }

        await using var fileStream = file.OpenReadStream();

        var docId = Guid.CreateVersion7();
        var uniqueFileName = $"{docId}{Path.GetExtension(file.FileName)}";

        // Save directly to upload/documents path instead of temp
        var directoryPath = Path.Combine(
            GetStorageBasePath(),
            _fileStorageConfiguration.RootPath.TrimStart('/'),
            _fileStorageConfiguration.DocumentsPath);

        logger.LogDebug("Saving file to directory: {DirectoryPath}", directoryPath);

        var (storagePath, checksum, _) = await CopyAndHashAsync(
            fileStream, directoryPath, uniqueFileName, cancellationToken: cancellationToken);

        logger.LogDebug("File saved to: {StoragePath} (checksum {Checksum})", storagePath, checksum);

        // From here on the bytes are on the share, so anything that goes wrong has to take them
        // with it on the way out.
        try
        {
            // An image the server cannot decode without exhausting its memory is refused here,
            // where the only thing wasted is the copy just made. Stored, it would sit as a trap
            // for whoever opens a gallery later — and the API is hosted in-process, so that takes
            // the whole site on that node down with it, not just the request.
            //
            // A multipart part is allowed to declare no Content-Type at all, and one written by
            // hand — an integration client rather than a browser — often does. ASP.NET Core hands
            // that over as an empty string, which the document's own constructor rejects, so it
            // has to be given a type here rather than passed along as it came.
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && !imageResizeService.IsWithinDecodeBudget(storagePath, out var refusal))
                throw new BadRequestException(refusal);

            return await CreateDocumentRecordAsync(
                docId, uploadSession, documentType, documentCategory, description,
                file.FileName, Path.GetExtension(file.FileName), file.Length,
                contentType,
                storagePath, uniqueFileName, checksum, cancellationToken);
        }
        catch
        {
            TryDeleteFile(storagePath);
            throw;
        }
    }

    /// <summary>
    /// The half of an upload that is the same however the bytes arrived: count them against the
    /// session, write the row, and answer with it.
    /// </summary>
    private async Task<UploadDocumentResult> CreateDocumentRecordAsync(
        Guid docId,
        UploadSession uploadSession,
        string documentType,
        string documentCategory,
        string? description,
        string fileName,
        string fileExtension,
        long fileSizeBytes,
        string contentType,
        string storagePath,
        string uniqueFileName,
        string checksum,
        CancellationToken cancellationToken)
    {
        uploadSession.IncrementDocumentCount(fileSizeBytes);

        // Generate storage URL
        var storageUrl =
            $"/{_fileStorageConfiguration.RootPath.TrimStart('/')}/{_fileStorageConfiguration.DocumentsPath}/{uniqueFileName}";

        var username = currentUserService.Username ?? "anonymous";

        logger.LogDebug("Creating document record for user {Username}", username);

        var document = Domain.Documents.Models.Document.Create(
            docId,
            uploadSession.Id,
            documentType,
            documentCategory,
            fileName,
            fileExtension,
            fileSizeBytes,
            contentType,
            storagePath,
            storageUrl,
            username,
            username,
            dateTimeProvider.ApplicationNow,
            description,
            null,
            null,
            checksum,
            "SHA256"
        );

        await _documentRepository.AddAsync(document, cancellationToken);

        logger.LogInformation(
            "Successfully uploaded document {DocumentId} for session {SessionId}",
            document.Id,
            uploadSession.Id);

        return new UploadDocumentResult(
            document.Id,
            document.FileName,
            document.FileExtension,
            document.FileSizeBytes,
            document.MimeType,
            document.StorageUrl,
            document.DocumentType,
            document.DocumentCategory,
            document.Description,
            document.UploadedBy,
            document.UploadedByName,
            document.UploadedAt
        );
    }

    /// <summary>
    /// Creates a Document from raw bytes (e.g. a server-generated PDF) without an IFormFile.
    /// Uses the same storage path and checksum logic as <see cref="UploadAsync"/>.
    /// A synthetic upload session id is generated internally.
    /// </summary>
    public async Task<Guid> UploadFromBytesAsync(
        byte[] bytes,
        string fileName,
        string mimeType,
        string documentType,
        string documentCategory,
        string uploadedBy,
        string? uploadedByName,
        CancellationToken cancellationToken = default)
    {
        var docId = Guid.CreateVersion7();
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{docId}{extension}";

        var directoryPath = Path.Combine(
            GetStorageBasePath(),
            _fileStorageConfiguration.RootPath.TrimStart('/'),
            _fileStorageConfiguration.DocumentsPath);

        await using var stream = new MemoryStream(bytes);
        var (storagePath, checksum, _) = await CopyAndHashAsync(
            stream, directoryPath, uniqueFileName, cancellationToken: cancellationToken);

        var storageUrl =
            $"/{_fileStorageConfiguration.RootPath.TrimStart('/')}/{_fileStorageConfiguration.DocumentsPath}/{uniqueFileName}";

        // Create a real UploadSession row first — Document.UploadSessionId is a NON-nullable FK
        // to UploadSessions with DeleteBehavior.Restrict, so the session must exist before the
        // Document row is inserted. Mirror the same expiry logic as CreateUploadSessionCommandHandler.
        var expiresAt = dateTimeProvider.ApplicationNow
            .AddHours(_fileStorageConfiguration.Cleanup.TempSessionExpirationHours);
        var session = UploadSession.Create(expiresAt, userAgent: null, ipAddress: null);
        await _uploadSessionRepository.AddAsync(session, cancellationToken);

        var document = Domain.Documents.Models.Document.Create(
            docId,
            session.Id,
            documentType,
            documentCategory,
            fileName,
            string.IsNullOrEmpty(extension) ? ".bin" : extension,
            bytes.LongLength,
            mimeType,
            storagePath,
            storageUrl,
            uploadedBy,
            uploadedByName ?? uploadedBy,
            dateTimeProvider.ApplicationNow,
            description: null,
            tags: null,
            customMetadata: null,
            checksum,
            "SHA256"
        );

        await _documentRepository.AddAsync(document, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created document {DocumentId} from bytes ({FileName}, {Bytes} bytes)",
            document.Id, fileName, bytes.LongLength);

        return document.Id;
    }

    /// <summary>
    /// Copies the upload to its final path and computes its SHA-256 in the same pass.
    /// </summary>
    /// <remarks>
    /// The file used to be copied and then read back to be hashed. At a few megabytes that was
    /// invisible; at a gigabyte over SMB it is a second trip across the wire for bytes we had in
    /// our hands moments earlier. Hashing as the bytes go past costs nothing and removes it.
    /// <para>
    /// The buffer is a megabyte rather than the framework's 80 KB because the destination is a
    /// network share: every write is a round trip, and the default turns a 1 GB copy into thirteen
    /// thousand of them. Memory stays flat regardless of file size — one buffer, reused.
    /// </para>
    /// </remarks>
    private async Task<(string StoragePath, string ChecksumBase64, long Length)> CopyAndHashAsync(
        Stream source,
        string directoryPath,
        string uniqueFileName,
        long maxBytes = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directoryPath);
        var fullPath = Path.Combine(directoryPath, uniqueFileName);

        const int bufferSize = 1024 * 1024;

        // FileMode.CreateNew, not Create: the name carries a fresh GUIDv7, so a file already
        // sitting there means something is wrong — overwriting it would destroy a stored document
        // silently. Opened outside the try below on purpose: that block deletes the file on the
        // way out, and the one failure this open can produce is "it is already there", where
        // deleting it would destroy exactly the document CreateNew is refusing to overwrite.
        var destination = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            long written = 0;

            await using (destination)
            {
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)) > 0)
                {
                    written += read;

                    // Counted as it goes, because a streamed body does not have to say how long it
                    // is up front — and when it does say, it is free to be lying.
                    if (written > maxBytes)
                        throw new BadRequestException(
                            $"File too large. Maximum size is {maxBytes / (1024 * 1024)}MB");

                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            return (fullPath, Convert.ToBase64String(hash.GetHashAndReset()), written);
        }
        catch
        {
            // A copy that died partway leaves a file that looks like a document and is not one.
            // Nothing sweeps this directory, so a half-written file would stay there for good —
            // and the browser closing mid-upload makes this the ordinary case, not the rare one.
            await destination.DisposeAsync();
            TryDeleteFile(fullPath);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void TryDeleteFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            // Losing the cleanup must not replace the error that caused it.
            logger.LogWarning(ex, "Could not remove incomplete upload at {StoragePath}", fullPath);
        }
    }

    public async Task CopyToAsync(string sourcePath, string destinationPath, bool deleteSource = false,
        CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (destinationDirectory is not null) Directory.CreateDirectory(destinationDirectory);

        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        if (deleteSource) File.Delete(sourcePath);
    }

    public async Task<bool> DeleteFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
            throw new NotFoundException($"Document {id} not found");

        var username = currentUserService.Username ?? "anonymous";
        document.Delete(username);

        await _documentRepository.UpdateAsync(document, cancellationToken);

        if (File.Exists(document.StoragePath))
        {
            File.Delete(document.StoragePath);
            logger.LogInformation("Physical file deleted at {StoragePath}", document.StoragePath);
        }
        else
        {
            logger.LogWarning("Physical file not found at {StoragePath} for document {DocumentId}",
                document.StoragePath, id);
        }

        logger.LogInformation("Document {DocumentId} deleted by {Username}", id, username);

        return true;
    }

    /// <summary>
    /// Writes a stream to the staging area without it passing through memory, and hands back what
    /// the caller needs to finish the job. For a body being read off the wire, where the metadata
    /// that belongs with the file may only arrive after it.
    /// </summary>
    public async Task<StagedUpload> StageStreamAsync(
        Stream content,
        string fileName,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        var documentId = Guid.CreateVersion7();
        var stagingPath = chunkedUploadStore.StreamedStagingPath(documentId);

        var (path, checksum, length) = await CopyAndHashAsync(
            content,
            Path.GetDirectoryName(stagingPath)!,
            Path.GetFileName(stagingPath),
            maxBytes,
            cancellationToken);

        return new StagedUpload(documentId, path, length, checksum);
    }


    /// <summary>
    /// Turns a file already on the share into a document — the last step of a chunked upload, and
    /// of a streamed one — without the bytes passing through here again.
    /// </summary>
    public async Task<UploadDocumentResult> CreateFromStagedFileAsync(
        Guid documentId,
        string stagedFilePath,
        StagedFileMetadata metadata,
        string? knownChecksumBase64 = null,
        CancellationToken cancellationToken = default)
    {
        var uploadSession =
            await _uploadSessionRepository.GetByIdAsync(metadata.UploadSessionId, cancellationToken);
        if (uploadSession is null)
            throw new NotFoundException($"Upload session {metadata.UploadSessionId} not found");

        var extension = Path.GetExtension(metadata.FileName);
        var uniqueFileName = $"{documentId}{extension}";
        var directoryPath = DocumentsDirectory();
        Directory.CreateDirectory(directoryPath);

        var storagePath = Path.Combine(directoryPath, uniqueFileName);

        // Staging and documents are folders on the same share, so this is a rename: a gigabyte
        // costs what a byte costs. Moving only on the first attempt — a completion retried after
        // its answer was lost finds the file already in place.
        try
        {
            File.Move(stagedFilePath, storagePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            if (!File.Exists(storagePath)) throw;
            logger.LogInformation(
                "Staged upload {DocumentId} was already moved into place — continuing", documentId);
        }

        try
        {
            // Hashed here only when nobody has done it already. A streamed upload hashes as it
            // writes and the move is a rename, so those bytes are the same bytes — reading a
            // gigabyte back across the share to confirm what we just computed buys nothing. A
            // chunked upload arrives in pieces with no running hash, so it is hashed here.
            var checksum = knownChecksumBase64
                           ?? await HashFileAsync(storagePath, cancellationToken);

            // The declared size, not FileInfo.Length: the caller has already checked the assembled
            // file against it, and over SMB a cached directory entry can answer with a stale
            // length — including zero, which the session counter refuses outright.
            var fileSizeBytes = metadata.FileSizeBytes;

            if (metadata.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && !imageResizeService.IsWithinDecodeBudget(storagePath, out var refusal))
                throw new BadRequestException(refusal);

            return await CreateDocumentRecordAsync(
                documentId, uploadSession, metadata.DocumentType, metadata.DocumentCategory,
                metadata.Description, metadata.FileName, extension, fileSizeBytes, metadata.ContentType,
                storagePath, uniqueFileName, checksum, cancellationToken);
        }
        catch
        {
            TryDeleteFile(storagePath);
            throw;
        }
    }

    private string DocumentsDirectory() => Path.Combine(
        GetStorageBasePath(),
        _fileStorageConfiguration.RootPath.TrimStart('/'),
        _fileStorageConfiguration.DocumentsPath);

    /// <summary>Hashes a file already on disk — one read, a megabyte at a time.</summary>
    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

        try
        {
            await using var file = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

            int read;
            while ((read = await file.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                hash.AppendData(buffer, 0, read);

            return Convert.ToBase64String(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private string GetStorageBasePath() =>
        DocumentStorage.BasePath(_fileStorageConfiguration, webHostEnvironment);
}