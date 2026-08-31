namespace Appraisal.Domain.Projects;

/// <summary>
/// Repository interface for the Project aggregate root.
/// Implementations live in Infrastructure; read-side queries use Dapper directly.
/// </summary>
public interface IProjectRepository
{
    /// <summary>Gets a project by its own Id (tracked for mutations).</summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets a project by the linked AppraisalId (tracked for mutations).</summary>
    Task<Project?> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default);

    /// <summary>
    /// Gets a project with all child collections eagerly loaded
    /// (towers, models + their sub-collections, units, unit uploads, unit prices,
    /// pricing assumption + model assumptions, land + land titles).
    /// </summary>
    Task<Project?> GetWithFullGraphAsync(Guid appraisalId, CancellationToken ct = default);

    /// <summary>
    /// Gets a project model by its own Id with the Images collection eagerly loaded.
    /// Used by image command handlers that need to mutate the model without loading the full project graph.
    /// </summary>
    Task<ProjectModel?> GetModelByIdWithImagesAsync(Guid modelId, CancellationToken ct = default);

    /// <summary>
    /// Gets a project tower by its own Id with the Images collection eagerly loaded.
    /// Used by image command handlers that need to mutate the tower without loading the full project graph.
    /// </summary>
    Task<ProjectTower?> GetTowerByIdWithImagesAsync(Guid towerId, CancellationToken ct = default);

    /// <summary>Stages a new project for insertion.</summary>
    void Add(Project project);

    /// <summary>Marks a project as modified (no-op for EF Core change-tracked entities).</summary>
    void Update(Project project);
}
