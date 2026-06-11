using Request.Contracts.Requests.Dtos;

namespace Collateral.Application.Features.Reappraisal.InitiateReappraisal;

/// <summary>
/// Creates one reappraisal request per candidate / in-system appraisal and assigns them all
/// the same group number. Returns the generated group number, created request IDs, and any
/// items skipped because a non-terminal reappraisal already exists (Layer 1 dedupe).
///
/// Two input paths:
///   <see cref="CandidateIds"/>       — ReappraisalCandidate rows (Status=Pending);
///                                      each resolves to a Request using the candidate's data.
///   <see cref="NearbyAppraisalIds"/> — In-system Appraisal IDs picked directly from the nearby
///                                      list (Source="InSystem" rows with no candidate row).
///
/// NOTE: As of Part 3 this command will be wired to an outbox event.
/// Until then the handler is a placeholder.
/// </summary>
public record InitiateReappraisalCommand(
    List<Guid> CandidateIds,
    List<Guid> NearbyAppraisalIds,
    UserInfoDto Requestor,
    UserInfoDto Creator
) : ICommand<InitiateReappraisalResult>;
