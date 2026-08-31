namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Re-matches an updated units Excel against the units of a block reappraisal project.
/// Unlike <c>UploadProjectUnits</c> (which replaces all units) this never wipes the unit list: it
/// matches on business keys and updates in place.
/// </summary>
/// <param name="ConfirmUpdates">
///   Whether the caller has seen, and accepted, the changes the Excel would make beyond sold/unsold
///   status — attribute edits to existing units, and units the Excel adds. Without it those two are
///   refused, which is the long-standing behaviour for attribute differences; the flag is how a
///   caller opts in to them after reviewing the preview.
/// </param>
public record UploadBlockReappraisalUnitsCommand(
    Guid AppraisalId,
    string FileName,
    Guid? DocumentId,
    Stream FileStream,
    bool ConfirmUpdates = false
) : ICommand<UploadBlockReappraisalUnitsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
