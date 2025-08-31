using Document.Contracts.Documents.Dtos;
using Document.Contracts.Documents.Services;
using Document.Data.Repository;
using Mapster;

namespace Document.Documents.Services;

public class DocumentValidationService : IDocumentValidationService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<DocumentValidationService> _logger;

    public DocumentValidationService(
        IDocumentRepository documentRepository,
        ILogger<DocumentValidationService> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<bool> DocumentExistsAsync(long documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking if document {DocumentId} exists", documentId);
            
            var document = await _documentRepository.GetDocumentById(documentId, true, cancellationToken);
            var exists = document != null;
            
            _logger.LogDebug("Document {DocumentId} exists: {Exists}", documentId, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if document {DocumentId} exists", documentId);
            return false;
        }
    }

    public async Task<DocumentDto?> GetDocumentAsync(long documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting document {DocumentId}", documentId);
            
            var document = await _documentRepository.GetDocumentById(documentId, true, cancellationToken);
            if (document == null)
            {
                _logger.LogDebug("Document {DocumentId} not found", documentId);
                return null;
            }

            var documentDto = document.Adapt<DocumentDto>();
            _logger.LogDebug("Retrieved document {DocumentId}: {Filename}", documentId, document.Filename);
            return documentDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<bool> CanLinkToRequestAsync(long documentId, string relateRequest, long relateId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking if document {DocumentId} can be linked to {RelateRequest}:{RelateId}", 
                documentId, relateRequest, relateId);

            var document = await _documentRepository.GetDocumentById(documentId, true, cancellationToken);
            if (document == null)
            {
                _logger.LogWarning("Cannot link non-existent document {DocumentId} to {RelateRequest}:{RelateId}", 
                    documentId, relateRequest, relateId);
                return false;
            }

            // Business rule: Check if document is already linked to a different request
            if (!string.IsNullOrEmpty(document.RelateRequest) && document.RelateId > 0)
            {
                if (document.RelateRequest != relateRequest || document.RelateId != relateId)
                {
                    _logger.LogWarning("Document {DocumentId} is already linked to {ExistingRelateRequest}:{ExistingRelateId}, cannot link to {RelateRequest}:{RelateId}",
                        documentId, document.RelateRequest, document.RelateId, relateRequest, relateId);
                    return false;
                }
            }

            // Additional business rules can be added here
            // For example: Check document type compatibility with request type
            // if (document.DocType == "Restricted" && relateRequest != "HighSecurity")
            //     return false;

            _logger.LogDebug("Document {DocumentId} can be linked to {RelateRequest}:{RelateId}", 
                documentId, relateRequest, relateId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if document {DocumentId} can be linked to {RelateRequest}:{RelateId}", 
                documentId, relateRequest, relateId);
            return false;
        }
    }
}