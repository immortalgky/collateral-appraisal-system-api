namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Admin correction of descriptive property data on a closed (Completed/Cancelled) appraisal.
/// <see cref="Data"/> follows null-means-unchanged semantics; <see cref="Reason"/> is mandatory and
/// is stored on the audit row.
/// </summary>
public record CorrectPropertyDataCommand(
    Guid AppraisalId,
    Guid PropertyId,
    string Reason,
    PropertyCorrectionData Data) : ICommand<CorrectPropertyDataResult>;
