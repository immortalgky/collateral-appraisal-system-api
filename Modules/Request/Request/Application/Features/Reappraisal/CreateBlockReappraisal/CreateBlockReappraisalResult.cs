namespace Request.Application.Features.Reappraisal.CreateBlockReappraisal;

/// <summary>
/// Result of the CreateBlockReappraisal command.
/// When <see cref="Skipped"/> is true the request was NOT created; see <see cref="SkipReason"/>.
/// </summary>
public record CreateBlockReappraisalResult(
    Guid? CreatedRequestId,
    string? RequestNumber,
    string GroupNumber,
    bool Skipped,
    string? SkipReason
);
