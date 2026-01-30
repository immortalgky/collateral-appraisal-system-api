using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Committees.CreateCommittee;

/// <summary>
/// Command to create a new Committee
/// </summary>
public record CreateCommitteeCommand(
    string CommitteeName,
    string CommitteeCode,
    string QuorumType,
    int QuorumValue,
    string MajorityType,
    string? Description = null
) : ICommand<CreateCommitteeResult>, ITransactionalCommand<IAppraisalUnitOfWork>;