using Appraisal.Application.Features.Project.GetProjectModels;

namespace Appraisal.Application.Features.Project.GetProjectModelById;

/// <summary>Handler for getting a single project model by ID.</summary>
public class GetProjectModelByIdQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectModelByIdQuery, GetProjectModelByIdResult>
{
    public async Task<GetProjectModelByIdResult> Handle(
        GetProjectModelByIdQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(query.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {query.AppraisalId}");

        var model = project.Models.FirstOrDefault(m => m.Id == query.ModelId)
                    ?? throw new InvalidOperationException($"Project model {query.ModelId} not found");

        return new GetProjectModelByIdResult(GetProjectModelsQueryHandler.MapToDto(model));
    }
}
