using Shared.Data;

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Lightweight projection of a ProjectModel's PricingAnalysis — only the fields
/// needed by read-side query handlers. Avoids loading the full PA graph.
/// </summary>
public record ProjectModelPricingSummary(
    Guid PricingAnalysisId,
    string Status,
    decimal? FinalAppraisedValue
);

/// <summary>
/// Repository interface for PricingAnalysis aggregate.
/// </summary>
public interface IPricingAnalysisRepository : IRepository<PricingAnalysis, Guid>
{
    /// <summary>Get pricing analysis for a PropertyGroup (SubjectType==PropertyGroup, AnchorId==id).</summary>
    Task<PricingAnalysis?> GetByPropertyGroupIdAsync(Guid propertyGroupId, CancellationToken cancellationToken = default);

    /// <summary>Get pricing analysis with all related data (approaches, methods, calculations, factor scores, etc.).</summary>
    Task<PricingAnalysis?> GetByIdWithAllDataAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Check if a pricing analysis exists for a property group.</summary>
    Task<bool> ExistsByPropertyGroupIdAsync(Guid propertyGroupId, CancellationToken cancellationToken = default);

    /// <summary>Get pricing analysis for a ProjectModel (SubjectType==ProjectModel, AnchorId==id).</summary>
    Task<PricingAnalysis?> GetByProjectModelIdAsync(Guid projectModelId, CancellationToken cancellationToken = default);

    /// <summary>Check if a pricing analysis exists for a project model.</summary>
    Task<bool> ExistsByProjectModelIdAsync(Guid projectModelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a summary (PricingAnalysisId, Status, FinalAppraisedValue) for each ProjectModel id
    /// that has a PricingAnalysis row. Models without a PA are absent from the result.
    /// Keyed by ProjectModel id (= AnchorId where SubjectType==ProjectModel).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ProjectModelPricingSummary>> GetProjectModelPricingSummariesAsync(
        IEnumerable<Guid> modelIds,
        CancellationToken cancellationToken = default);

    // ── Reference analysis methods ────────────────────────────────────────────

    /// <summary>
    /// List all reference PricingAnalyses for a given anchor (and optional refKey).
    /// Returns analyses of any Ref subtype with AnchorId==anchorId [and AnchorRefKey==anchorRefKey].
    /// </summary>
    Task<IReadOnlyList<PricingAnalysis>> GetReferencesByAnchorAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find an exact reference match by (SubjectType, AnchorId, AnchorRefKey).
    /// Used by the idempotent CreateOrGetReference endpoint.
    /// </summary>
    Task<PricingAnalysis?> FindReferenceAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        CancellationToken cancellationToken = default);

    // ── Bulk-delete helpers for active cleanup (DL10) ─────────────────────────

    /// <summary>
    /// Returns all PricingAnalysisMethod ids belonging to the subject PricingAnalysis
    /// identified by (SubjectType, AnchorId). Used by PricingReferenceCleanupService to
    /// find hosted reference analyses without loading the full PA graph.
    /// Returns an empty list if no PA exists for the anchor.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetMethodIdsForSubjectAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all PricingAnalysisMethod ids belonging to the PricingAnalysis with the given Id
    /// (its approaches → methods). Used by the group-level References section to find references
    /// hosted by any of a group's methods.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetMethodIdsByAnalysisIdAsync(
        Guid pricingAnalysisId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List reference PricingAnalyses whose HostMethodId is in the given set, eager-loading
    /// Approaches → Methods → FinalValue so method summaries (type + computed value) can be projected.
    /// </summary>
    Task<IReadOnlyList<PricingAnalysis>> GetReferencesByHostMethodIdsAsync(
        IEnumerable<Guid> hostMethodIds,
        CancellationToken cancellationToken = default);

    /// <summary>Delete all reference PricingAnalyses whose HostMethodId is in the given set.</summary>
    Task DeleteByHostMethodIdsAsync(IEnumerable<Guid> methodIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete reference PricingAnalyses by anchor coordinates.
    /// Deletes rows where SubjectType==subjectType AND AnchorId==anchorId [AND AnchorRefKey==anchorRefKey].
    /// Pass anchorRefKey=null to delete ALL refs for that anchor regardless of refKey.
    /// </summary>
    Task DeleteReferencesByAnchorAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes RoomIncomeRef PricingAnalyses whose HostMethodId == <paramref name="hostMethodId"/>
    /// AND AnchorRefKey is NOT in <paramref name="keepCodes"/>.
    /// Used by PricingReferenceCleanupService when room types are removed from a Method01 save.
    /// </summary>
    Task DeleteRoomRefsByHostMethodExceptCodesAsync(
        Guid hostMethodId,
        IReadOnlyCollection<string> keepCodes,
        CancellationToken cancellationToken = default);
}
