using Appraisal.Application.Features.Project.GetProjectTowers;

namespace Appraisal.Application.Features.Project.GetProjectTowerById;

/// <summary>Handler for getting a single project tower by ID.</summary>
public class GetProjectTowerByIdQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectTowerByIdQuery, GetProjectTowerByIdResult>
{
    public async Task<GetProjectTowerByIdResult> Handle(
        GetProjectTowerByIdQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(query.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {query.AppraisalId}");

        var tower = project.Towers.FirstOrDefault(t => t.Id == query.TowerId)
                    ?? throw new InvalidOperationException($"Project tower {query.TowerId} not found");

        return new GetProjectTowerByIdResult(GetProjectTowersQueryHandler.MapToDto(tower));
    }
}
