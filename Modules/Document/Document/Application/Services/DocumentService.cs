using Document.Domain.Documents;
using Document.Domain.Documents.Models;
using Document.Domain.UploadSessions.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Shared.Configurations;
using Shared.Data;
using Shared.Identity;

namespace Document.Services;

public class DocumentService(
    ICurrentUserService currentUserService,
    IDocumentUnitOfWork uow,
    IWebHostEnvironment webHostEnvironment,
    IOptions<FileStorageConfiguration> fileStorageOptions,
    ILogger<DocumentService> logger)
    : IDocumentService
{
    private readonly FileStorageConfiguration _fileStorageConfiguration = fileStorageOptions.Value;

    private readonly IRepository<Domain.Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Domain.Documents.Models.Document, Guid>();

    private readonly IRepository<UploadSession, Guid> _uploadSessionRepository = uow.Repository<UploadSession, Guid>();

    public async Task<Guid> UploadAsync(IFormFile file, Guid uploadSessionId, string documentType,
        string documentCategory, string? description, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Uploading file {FileName} ({FileSize} bytes) to session {SessionId}",
            file.FileName,
            file.Length,
            uploadSessionId);

        await using var fileStream = file.OpenReadStream();

        var docId = Guid.NewGuid();
        var uniqueFileName = $"{docId}{Path.GetExtension(file.FileName)}";

        // Save directly to upload/documents path instead of temp
        var directoryPath = Path.Combine(
            webHostEnvironment.WebRootPath,
            _fileStorageConfiguration.RootPath.TrimStart('/'),
            _fileStorageConfiguration.DocumentsPath);

        logger.LogDebug("Saving file to directory: {DirectoryPath}", directoryPath);

        var storagePath = await SaveFileAsync(fileStream, directoryPath, uniqueFileName, cancellationToken);

        logger.LogDebug("File saved to: {StoragePath}", storagePath);

        fileStream.Seek(0, SeekOrigin.Begin);
        var checksum = await CalculateChecksumAsync(fileStream, cancellationToken);

        logger.LogDebug("File checksum: {Checksum}", checksum);

        var uploadSession = await _uploadSessionRepository.GetByIdAsync(uploadSessionId, cancellationToken);
        if (uploadSession is null)
        {
            logger.LogError("Upload session {SessionId} not found", uploadSessionId);
            throw new NotFoundException($"Upload session {uploadSessionId} not found");
        }

        uploadSession.IncrementDocumentCount(file.Length);

        // Generate storage URL
        var storageUrl =
            $"/{_fileStorageConfiguration.RootPath.TrimStart('/')}/{_fileStorageConfiguration.DocumentsPath}/{uniqueFileName}";

        var username = currentUserService.Username ?? "anonymous";
        var userId = currentUserService.UserId?.ToString() ?? "anonymous";

        logger.LogDebug(
            "Creating document record for user {Username} ({UserId})",
            username,
            userId);

        var document = Domain.Documents.Models.Document.Create(
            docId,
            uploadSession.Id,
            documentType,
            documentCategory,
            file.FileName,
            Path.GetExtension(file.FileName),
            fileStream.Length,
            file.ContentType,
            storagePath,
            storageUrl,
            userId,
            username,
            DateTime.UtcNow,
            description,
            null,
            null,
            checksum,
            "SHA256"
        );

        await _documentRepository.AddAsync(document, cancellationToken);
        //await uow.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Successfully uploaded document {DocumentId} for session {SessionId}",
            document.Id,
            uploadSessionId);

        return document.Id;
    }

    private async Task<string> SaveFileAsync(Stream fileStream, string directoryPath, string uniqueFileName,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directoryPath);

        var fullPath = Path.Combine(directoryPath, uniqueFileName);
        await using var file = File.Create(fullPath);
        await fileStream.CopyToAsync(file, cancellationToken);

        return fullPath;
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

    public Task<bool> DeleteFileAsync(long id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<string> CalculateChecksumAsync(Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToBase64String(hash);
    }
}