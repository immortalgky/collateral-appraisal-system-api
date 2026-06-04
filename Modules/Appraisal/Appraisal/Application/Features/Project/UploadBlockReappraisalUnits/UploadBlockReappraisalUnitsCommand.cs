namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Re-matches an updated units Excel against the seeded units of a block reappraisal project.
/// Unlike <c>UploadProjectUnits</c> (which replaces all units), this command only updates sold/unsold
/// status by matching business keys — it does not wipe existing units or their sale flags.
/// </summary>
public record UploadBlockReappraisalUnitsCommand(
    Guid AppraisalId,
    string FileName,
    Guid? DocumentId,
    Stream FileStream
) : ICommand<UploadBlockReappraisalUnitsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
