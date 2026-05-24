namespace Appraisal.Application.Services;

/// <summary>
/// Shared create-or-update logic for the Project aggregate, reused by both the
/// final-save and draft-save command handlers. Validation strictness is decided
/// upstream by each command's validator, not here.
/// </summary>
public interface IProjectSaveService
{
    /// <summary>
    /// Creates the Project for the appraisal if none exists, otherwise updates it.
    /// ProjectType is immutable after creation. Returns the Project id.
    /// </summary>
    Task<Guid> SaveAsync(SaveProjectData data, CancellationToken cancellationToken);
}
