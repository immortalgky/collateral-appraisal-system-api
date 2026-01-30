namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentType;

/// <summary>
/// Result of creating a document type
/// </summary>
public record CreateDocumentTypeResult(Guid Id, string Code, string Name);
