using Appraisal.Application.Features.Project.GetProjectModels;

namespace Appraisal.Application.Features.Project.GetProjectModelById;

/// <summary>Result containing a single project model.</summary>
public record GetProjectModelByIdResult(ProjectModelDto Model);
