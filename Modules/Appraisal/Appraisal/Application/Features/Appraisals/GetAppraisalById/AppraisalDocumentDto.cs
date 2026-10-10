using Appraisal.Contracts.Appraisals;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// One file of the appraisal's request (<c>documents</c> of GET /appraisals/{id}?include=documents).
/// <c>DocumentType</c> is the code the file carries here; <c>SuggestedType</c> is the code a new request should file it
/// under (D036 for the D042/D043 summary report, otherwise the same). The consumer maps the files itself.
/// </summary>
/// <param name="DocumentId">Null for a caller who may not download it yet (see <c>AppraisalFieldScope</c>); so is <c>FilePath</c>, since the share is served as static files.</param>
/// <param name="Level">"Request" or "Title".</param>
/// <param name="TitleId">Title-level files: this appraisal's request title. Information only - match titles by the key (<c>CollateralType</c> + <c>TitleNumber</c>).</param>
public record AppraisalDocumentDto(
    Guid? DocumentId,
    string DocumentType,
    string SuggestedType,
    string Level,
    Guid? TitleId,
    string? CollateralType,
    string? TitleNumber,
    string? FileName,
    string? FilePath,
    string? Prefix,
    int Set,
    string? Notes,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt,
    bool DefaultUse)
{
    /// <summary>From the carry-forward read: its source code becomes the document type, its (re-typed) code the suggestion.</summary>
    public static AppraisalDocumentDto From(CarryForwardDocumentDto d, bool withholdDocumentId) => new(
        withholdDocumentId ? null : d.DocumentId,
        d.SourceDocumentType,
        d.DocumentType,
        d.Level,
        d.PriorTitleId,
        d.CollateralType,
        d.TitleNumber,
        d.FileName,
        withholdDocumentId ? null : d.FilePath,
        d.Prefix,
        d.Set,
        d.Notes,
        d.UploadedBy,
        d.UploadedByName,
        d.UploadedAt,
        d.DefaultUse);
}
