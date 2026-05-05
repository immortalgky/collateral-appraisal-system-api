using Shared.Data;

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Repository interface for PricingAnalysis aggregate
/// </summary>
public interface IPricingAnalysisRepository : IRepository<PricingAnalysis, Guid>
{
    /// <summary>
    /// Get pricing analysis by PropertyGroup ID
    /// </summary>
    Task<PricingAnalysis?> GetByPropertyGroupIdAsync(Guid propertyGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pricing analysis with all related data (approaches, methods, calculations, factor scores)
    /// </summary>
    Task<PricingAnalysis?> GetByIdWithAllDataAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if pricing analysis exists for a property group
    /// </summary>
    Task<bool> ExistsByPropertyGroupIdAsync(Guid propertyGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pricing analysis by ProjectModel ID (model-level analysis)
    /// </summary>
    Task<PricingAnalysis?> GetByProjectModelIdAsync(Guid projectModelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a pricing analysis exists for a project model
    /// </summary>
    Task<bool> ExistsByProjectModelIdAsync(Guid projectModelId, CancellationToken cancellationToken = default);
}
