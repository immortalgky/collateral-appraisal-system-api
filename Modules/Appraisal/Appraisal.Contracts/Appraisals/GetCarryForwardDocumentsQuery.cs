using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// The files of a Completed appraisal that a new request referencing it may reuse: every file of the
/// prior request (request level and per title) plus the prior appraisal's summary report re-typed
/// as D036. Throws 404 when the appraisal does not exist and 409 when it is not Completed.
/// Shared by the FE endpoint, the Integration endpoint and the system-created reappraisals.
/// </summary>
/// <param name="EnforceCallerScope">
/// Set only by the FE endpoint: an external (company) user then gets 404 for an appraisal not assigned to
/// their company. Off for Integration (LOS policy) and background callers, which have no company user.
/// </param>
public record GetCarryForwardDocumentsQuery(Guid AppraisalId, bool EnforceCallerScope = false)
    : IRequest<CarryForwardDocumentsResult>;

public record CarryForwardDocumentsResult(
    Guid AppraisalId,
    string? AppraisalNumber,
    IReadOnlyList<CarryForwardDocumentDto> Documents);

/// <param name="DocumentType">D036 for the summary report, otherwise the original code.</param>
/// <param name="SourceDocumentType">The code on the prior appraisal (D042/D043 for the summary report).</param>
/// <param name="Level">"Request" or "Title".</param>
/// <param name="PriorTitleId">The prior request's RequestTitles.Id for Title-level files — information only.</param>
/// <param name="CollateralType">Title-level files: the title's collateral type. With <c>TitleNumber</c> it is the title's key; callers match titles by the key, never by <c>PriorTitleId</c>.</param>
/// <param name="TitleNumber">Title-level files: that collateral type's number (deed / plate / registration / vessel).</param>
public record CarryForwardDocumentDto(
    Guid DocumentId,
    string DocumentType,
    string SourceDocumentType,
    string Level,
    Guid? PriorTitleId,
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
    bool DefaultUse);
