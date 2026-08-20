namespace Appraisal.Domain.Settings;

/// <summary>
/// Repository interface for AppraisalSettings.
/// </summary>
/// <remarks>
/// The AutoAssignmentRule entity that used to live alongside this one has moved to the Workflow
/// module (workflow.AutoAssignmentRules), which owns initial-routing. The Appraisal-side table was
/// never read at runtime and has been dropped.
/// </remarks>
public interface IAppraisalSettingsRepository : IRepository<AppraisalSettings, Guid>
{
    Task<AppraisalSettings?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalSettings>> GetAllSettingsAsync(CancellationToken cancellationToken = default);
}