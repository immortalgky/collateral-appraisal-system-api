namespace Appraisal.Domain.ComparativeAnalysis;

/// <summary>
/// Repository interface for ComparativeAnalysisTemplate aggregate
/// </summary>
public interface IComparativeAnalysisTemplateRepository
{
    Task<ComparativeAnalysisTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ComparativeAnalysisTemplate?> GetByIdWithFactorsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ComparativeAnalysisTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken cancellationToken = default);
    Task<ComparativeAnalysisTemplate?> GetByPropertyTypeAsync(string propertyType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComparativeAnalysisTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComparativeAnalysisTemplate>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByTemplateCodeAsync(string templateCode, CancellationToken cancellationToken = default);
    void Add(ComparativeAnalysisTemplate template);
    void Update(ComparativeAnalysisTemplate template);
    void Delete(ComparativeAnalysisTemplate template);
}
