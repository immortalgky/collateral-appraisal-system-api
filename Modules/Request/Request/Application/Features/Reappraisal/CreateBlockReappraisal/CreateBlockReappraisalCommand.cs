namespace Request.Application.Features.Reappraisal.CreateBlockReappraisal;

/// <summary>
/// Creates a single reappraisal request for one block-project collateral master
/// (Phase D — "Create New Appraisal Request" button on the block-reappraisal screen).
///
/// Mirrors <see cref="InitiateReappraisalCommand"/> but targets a single PrevAppraisalId
/// resolved by the Collateral module from <c>ProjectDetail.AppraisalSummary.LastAppraisalId</c>.
///
/// Layer-1 dedupe: if a non-terminal reappraisal already references PrevAppraisalId the
/// command returns Skipped=true rather than raising an error.
/// </summary>
public record CreateBlockReappraisalCommand(
    Guid PrevAppraisalId,
    UserInfoDto Requestor,
    UserInfoDto Creator
) : ICommand<CreateBlockReappraisalResult>, ITransactionalCommand<IRequestUnitOfWork>;
