using Document.Documents.Exceptions;
using Shared.Exceptions;

namespace Document.Documents.Services;

public class DocumentDomainService : IDocumentDomainService
{
    private readonly IDocumentSecurityService _securityService;
    private readonly ILogger<DocumentDomainService> _logger;

    public DocumentDomainService(
        IDocumentSecurityService securityService,
        ILogger<DocumentDomainService> logger)
    {
        _securityService = securityService;
        _logger = logger;
    }

    public async Task<Models.Document> CreateDocumentAsync(
        string relateRequest,
        long relateId,
        string docType,
        string filename,
        string prefix,
        short set,
        string comment,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating document {Filename} for {RelateRequest}:{RelateId}", 
            filename, relateRequest, relateId);

        // Validate business rules
        await ValidateDocumentCreationRules(relateRequest, relateId, filename, fileStream);

        // Perform security validation
        await ValidateDocumentSecurityAsync(fileStream, filename, cancellationToken);

        // Generate file path with hash
        var fileHash = await GenerateFileHash(fileStream);
        var fileExtension = Path.GetExtension(filename);
        var safeFilename = $"{fileHash}{fileExtension}";
        var uploadFolder = "Upload";
        var filePath = Path.Combine(uploadFolder, safeFilename);

        // Ensure upload directory exists
        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        // Check for duplicate files
        if (File.Exists(filePath))
        {
            throw new UploadDocumentException("Duplicate file detected. This file has already been uploaded.");
        }

        // Save file to disk
        await SaveStreamToFile(fileStream, filePath);

        // Create document entity
        var document = Models.Document.Create(
            relateRequest,
            relateId,
            docType,
            filename,
            DateTime.Now,
            prefix,
            set,
            comment,
            filePath
        );

        _logger.LogInformation("Document {Filename} created successfully with ID {DocumentId}", 
            filename, document.Id);

        return document;
    }

    public async Task ValidateDocumentSecurityAsync(Stream fileStream, string filename, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting security validation for file {Filename}", filename);

        try
        {
            // Reset stream position for scanning
            fileStream.Position = 0;

            // Perform virus scan
            var scanResult = await _securityService.ScanAsync(fileStream, filename);

            if (!scanResult.IsClean)
            {
                var errorMessage = scanResult.ThreatName ?? "Unknown security threat detected";
                _logger.LogWarning("Security validation failed for {Filename}: {ThreatName}", 
                    filename, errorMessage);
                
                throw new UploadDocumentException($"File security validation failed: {errorMessage}");
            }

            _logger.LogInformation("Security validation passed for {Filename}. Scan method: {ScanMethod}, Duration: {Duration}ms", 
                filename, scanResult.ScanMethod, scanResult.ScanDuration.TotalMilliseconds);
        }
        catch (UploadDocumentException)
        {
            throw; // Re-throw security validation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during security validation for {Filename}", filename);
            throw new UploadDocumentException($"Security validation error: {ex.Message}");
        }
        finally
        {
            // Reset stream position for subsequent operations
            fileStream.Position = 0;
        }
    }

    private async Task ValidateDocumentCreationRules(string relateRequest, long relateId, string filename, Stream fileStream)
    {
        var validationErrors = new List<string>();

        // Business rule: Valid relate request
        if (string.IsNullOrWhiteSpace(relateRequest))
        {
            validationErrors.Add("RelateRequest is required.");
        }

        // Business rule: Valid relate ID
        if (relateId <= 0)
        {
            validationErrors.Add("RelateId must be a positive number.");
        }

        // Business rule: Valid filename
        if (string.IsNullOrWhiteSpace(filename))
        {
            validationErrors.Add("Filename is required.");
        }
        else
        {
            // Check filename length
            if (filename.Length > 255)
            {
                validationErrors.Add("Filename cannot exceed 255 characters.");
            }

            // Check for invalid characters in filename
            var invalidChars = Path.GetInvalidFileNameChars();
            if (filename.Any(c => invalidChars.Contains(c)))
            {
                validationErrors.Add("Filename contains invalid characters.");
            }
        }

        // Business rule: File stream validation
        if (fileStream == null)
        {
            validationErrors.Add("File stream is required.");
        }
        else
        {
            // Check file size
            if (fileStream.Length <= 0)
            {
                validationErrors.Add("File is empty.");
            }
            else if (fileStream.Length > 5 * 1024 * 1024) // 5MB limit
            {
                validationErrors.Add("File size exceeds the maximum limit of 5MB.");
            }

            // Check file extension
            var fileExtension = Path.GetExtension(filename);
            var allowedExtensions = new[] { ".pdf", ".PDF" };
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                validationErrors.Add("Only PDF files are allowed.");
            }
        }

        // Business rule: Maximum files per relate request (check would require repository access)
        // This could be implemented by injecting repository and checking existing count

        if (validationErrors.Any())
        {
            var errorMessage = string.Join(" | ", validationErrors);
            _logger.LogWarning("Document creation validation failed: {ValidationErrors}", errorMessage);
            throw new DomainException($"Document creation failed: {errorMessage}");
        }

        await Task.CompletedTask;
    }

    private async Task<string> GenerateFileHash(Stream fileStream)
    {
        fileStream.Position = 0;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = await sha256.ComputeHashAsync(fileStream);
        fileStream.Position = 0;
        return Convert.ToHexString(hash);
    }

    private async Task SaveStreamToFile(Stream fileStream, string filePath)
    {
        fileStream.Position = 0;
        using var fileWriteStream = File.Create(filePath);
        await fileStream.CopyToAsync(fileWriteStream);
        fileStream.Position = 0;
    }
}