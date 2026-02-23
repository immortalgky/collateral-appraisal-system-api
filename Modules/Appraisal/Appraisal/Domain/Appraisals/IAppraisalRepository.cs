namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Repository interface for Appraisal aggregate
/// </summary>
public interface IAppraisalRepository : IRepository<Appraisal, Guid>
{
    /// <summary>
    /// Get appraisal by ID with all properties loaded
    /// </summary>
    Task<Appraisal?> GetByIdWithPropertiesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get appraisal by ID with all related data (properties, groups, assignments)
    /// </summary>
    Task<Appraisal?> GetByIdWithAllDataAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get appraisal by appraisal number
    /// </summary>
    Task<Appraisal?> GetByAppraisalNumberAsync(string appraisalNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get appraisals by request ID
    /// </summary>
    Task<IEnumerable<Appraisal>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if appraisal exists for request
    /// </summary>
    Task<bool> ExistsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all appraisals
    /// </summary>
    Task<IEnumerable<Appraisal>> GetAllAsync(CancellationToken cancellationToken = default);
}