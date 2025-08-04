using Document.Documents.Features.UploadDocument;

namespace Document.Services;

public class DocumentService(IDocumentRepository documentRepository) : IDocumentService
{
    private static readonly string[] PermittedExtensions = [".pdf", ".PDF"];
    private const long MaxSize = 5 * 1024 * 1024;
    private static readonly string UploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "Upload");
    private readonly IDocumentRepository _documentRepository = documentRepository;

    public async Task<UploadDocumentResult> UploadAsync(IReadOnlyList<IFormFile> files, string relateRequest, long relateId, CancellationToken cancellationToken = default)
    {
        if (files.Count > 5)
            throw new UploadDocumentException("File upload limit exceeded (maximum 5 files).");

        EnsureUploadDirectoryExists();
        var results = new List<UploadResultDto>();

        foreach (var file in files)
        {
            try
            {
                var (document, result) = await ProcessFileAsync(file, relateRequest, relateId, cancellationToken);
                await _documentRepository.UploadDocument(document, cancellationToken);
                results.Add(result);
            }
            catch (UploadDocumentException ex)
            {
                results.Add(new UploadResultDto(false, ex.Message));
            }
        }

        return new UploadDocumentResult(results);
    }

    public async Task<(Documents.Models.Document, UploadResultDto)> ProcessFileAsync(IFormFile file, string request, long id, CancellationToken cancellationToken = default)
    {
        ValidateFile(file);

        var fileExtension = Path.GetExtension(file.FileName);
        var hashedFileName = await GenerateHashedFileNameAsync(file, fileExtension, cancellationToken);
        var fullPath = Path.Combine(UploadFolder, hashedFileName);

        if (await _documentRepository.GetDocument(fullPath, request, id, true, cancellationToken))
            throw new UploadDocumentException("Duplicate file detected. This PDF has already been uploaded.");

        await SaveFileToDiskAsync(file, fullPath, cancellationToken);

        var document = Documents.Models.Document.Create(request, id, "DocType", hashedFileName, DateTime.Now, "Prefix", 1, "", fullPath);
        return (document, new UploadResultDto(true, "Success"));
    }

    public async Task<bool> DeleteFileAsync(long id, CancellationToken cancellationToken = default)
    {
        var file = await _documentRepository.GetDocumentById(id, true, cancellationToken);
        var result = await _documentRepository.DeleteDocument(id, cancellationToken);

        var isDeleted = await _documentRepository.DeleteDocument(file.FilePath, cancellationToken);

        if (!isDeleted && File.Exists(file.FilePath))
            File.Delete(file.FilePath);

        return result;
    }

    // === Utilities ===

    private static void EnsureUploadDirectoryExists()
    {
        if (!Directory.Exists(UploadFolder))
            Directory.CreateDirectory(UploadFolder);
    }

    private static void ValidateFile(IFormFile file)
    {
        if (file.Length <= 0)
            throw new UploadDocumentException("File is empty.");

        if (file.Length > MaxSize)
            throw new UploadDocumentException($"File size exceeded {MaxSize} bytes.");

        var fileExtension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(fileExtension) || !PermittedExtensions.Contains(fileExtension))
            throw new UploadDocumentException("File extension not allowed.");
    }

    private static async Task<string> GenerateHashedFileNameAsync(IFormFile file, string extension, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        stream.Seek(0, SeekOrigin.Begin);
        var hash = await SHA256.Create().ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash) + extension;
    }

    private static async Task SaveFileToDiskAsync(IFormFile file, string fullPath, CancellationToken cancellationToken)
    {
        if (File.Exists(fullPath)) return;

        try
        {
            using var stream = new FileStream(fullPath, FileMode.CreateNew);
            await file.CopyToAsync(stream, cancellationToken);
        }
        catch (IOException ex) when (IsDiskFull(ex))
        {
            throw new UploadDocumentException("Storage full. Cannot upload file at this time.");
        }
    }

    private static bool IsDiskFull(IOException ex)
    {
        return ex.HResult == unchecked((int)0x80070070); // ERROR_DISK_FULL
    }
}
