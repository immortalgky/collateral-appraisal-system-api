using Appraisal.Application.Services;
using Mapster;

namespace Appraisal.Application.Features.Project.SaveProject;

/// <summary>
/// Handler for saving (create or update) a Project aggregate (final save — full validation
/// runs in SaveProjectCommandValidator). Delegates the create-or-update work to the shared
/// <see cref="IProjectSaveService"/>, which is also used by the draft-save flow.
/// </summary>
public class SaveProjectCommandHandler(
    IProjectSaveService projectSaveService
) : ICommandHandler<SaveProjectCommand, SaveProjectResult>
{
    public async Task<SaveProjectResult> Handle(
        SaveProjectCommand command,
        CancellationToken cancellationToken)
    {
        var id = await projectSaveService.SaveAsync(command.Adapt<SaveProjectData>(), cancellationToken);
        return new SaveProjectResult(id);
    }
}
