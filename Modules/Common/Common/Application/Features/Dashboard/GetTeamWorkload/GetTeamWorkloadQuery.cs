using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetTeamWorkload;

public record GetTeamWorkloadQuery : IQuery<GetTeamWorkloadResult>;

public record GetTeamWorkloadResult(List<TeamWorkloadDto> Items);

public record TeamWorkloadDto
{
    public string Username { get; init; } = default!;
    public int NotStarted { get; init; }
    public int InProgress { get; init; }
    public int Completed { get; init; }
}
