using Appraisal.Application.Services;
using Mapster;

namespace Appraisal.Application.Features.Project.SaveProjectDraft;

/// <summary>
/// Handler for saving a project as a DRAFT. Delegates to the shared IProjectSaveService —
/// identical persistence to the final save; only the validator differs.
/// </summary>
public class SaveProjectDraftCommandHandler(
    IProjectSaveService projectSaveService
) : ICommandHandler<SaveProjectDraftCommand, SaveProjectDraftResult>
{
    public async Task<SaveProjectDraftResult> Handle(
        SaveProjectDraftCommand command,
        CancellationToken cancellationToken)
    {
        var id = await projectSaveService.SaveAsync(command.Adapt<SaveProjectData>(), cancellationToken);
        return new SaveProjectDraftResult(id);
    }
}
