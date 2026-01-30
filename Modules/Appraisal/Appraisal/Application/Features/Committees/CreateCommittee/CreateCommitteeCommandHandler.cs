using Appraisal.Domain.Committees;
using Shared.CQRS;

namespace Appraisal.Application.Features.Committees.CreateCommittee;

/// <summary>
/// Handler for creating a new Committee
/// </summary>
public class CreateCommitteeCommandHandler(
    ICommitteeRepository committeeRepository
) : ICommandHandler<CreateCommitteeCommand, CreateCommitteeResult>
{
    public async Task<CreateCommitteeResult> Handle(
        CreateCommitteeCommand command,
        CancellationToken cancellationToken)
    {
        var committee = Committee.Create(
            command.CommitteeName,
            command.CommitteeCode,
            command.QuorumType,
            command.QuorumValue,
            command.MajorityType);

        await committeeRepository.AddAsync(committee, cancellationToken);

        return new CreateCommitteeResult(committee.Id);
    }
}