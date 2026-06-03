using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using System.Text.Json;

namespace Appraisal.Application.Services;

/// <summary>
/// Active-cleanup service for reference PricingAnalyses (DL10).
/// Called within the same UoW/transaction from each delete/save handler so that
/// orphan reference rows are removed atomically with the triggering operation.
///
/// All methods use EF's RemoveRange on the tracked DbContext — no SaveChanges call;
/// the outer handler's transaction commits everything together.
/// </summary>
public class PricingReferenceCleanupService(IPricingAnalysisRepository repository)
{
    /// <summary>
    /// Deletes the subject PricingAnalysis for a PropertyGroup together with all
    /// reference PricingAnalyses whose HostMethodId is one of its methods' ids.
    /// </summary>
    public async Task CleanupForPropertyGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        // Fetch method ids via a lightweight projection (avoids loading the full PA graph).
        var methodIds = await repository.GetMethodIdsForSubjectAsync(
            PricingAnalysisSubjectType.PropertyGroup, groupId, ct);

        await repository.DeleteByHostMethodIdsAsync(methodIds, ct);

        // Delete the subject PA itself (EF cascade on PricingAnalysisApproaches handles children).
        await repository.DeleteReferencesByAnchorAsync(
            PricingAnalysisSubjectType.PropertyGroup, groupId, anchorRefKey: null, ct);
    }

    /// <summary>
    /// Deletes the subject PricingAnalysis for a ProjectModel together with all
    /// reference PricingAnalyses hosted by its methods.
    /// Replaces the former DB cascade FK from PricingAnalysis.ProjectModelId → ProjectModels.Id.
    /// </summary>
    public async Task CleanupForProjectModelAsync(Guid modelId, CancellationToken ct = default)
    {
        // Fetch method ids via a lightweight projection (avoids loading the full PA graph).
        var methodIds = await repository.GetMethodIdsForSubjectAsync(
            PricingAnalysisSubjectType.ProjectModel, modelId, ct);

        await repository.DeleteByHostMethodIdsAsync(methodIds, ct);

        // Delete the subject PA itself.
        await repository.DeleteReferencesByAnchorAsync(
            PricingAnalysisSubjectType.ProjectModel, modelId, anchorRefKey: null, ct);
    }

    /// <summary>
    /// Deletes all MachineryCostRef reference PricingAnalyses anchored to the given property
    /// (AppraisalProperty = machine detail).
    /// </summary>
    public async Task CleanupForPropertyAsync(Guid propertyId, CancellationToken ct = default)
    {
        await repository.DeleteReferencesByAnchorAsync(
            PricingAnalysisSubjectType.MachineryCostRef,
            propertyId,
            anchorRefKey: null,
            ct);
    }

    /// <summary>
    /// Deletes all reference PricingAnalyses whose HostMethodId equals the removed method id.
    /// Covers Income/Leasehold/ProfitRent method removal.
    /// </summary>
    public async Task CleanupForMethodRemovalAsync(Guid methodId, CancellationToken ct = default)
    {
        await repository.DeleteByHostMethodIdsAsync([methodId], ct);
    }

    /// <summary>
    /// Reconciles RoomIncomeRef reference analyses after a Method01 income save.
    /// Deletes references whose AnchorRefKey (room-type code) is no longer in
    /// <paramref name="remainingRoomCodes"/>, scoped to <paramref name="hostMethodId"/>.
    /// </summary>
    /// <param name="hostMethodId">
    ///   The PricingAnalysisMethod.Id of the income method that owns the room-income field.
    ///   RoomIncomeRef rows are created with HostMethodId set to this value.
    /// </param>
    /// <param name="remainingRoomCodes">
    ///   Raw room-type codes (the RoomType field value, e.g. "01", "99") still present in
    ///   the saved Method01 payload. AnchorRefKey on created refs matches these raw codes.
    /// </param>
    public async Task CleanupForIncomeRoomsAsync(
        Guid hostMethodId,
        IReadOnlyCollection<string> remainingRoomCodes,
        CancellationToken ct = default)
    {
        // Scope by HostMethodId: only touch RoomIncomeRef rows owned by this income method.
        await repository.DeleteRoomRefsByHostMethodExceptCodesAsync(
            hostMethodId, remainingRoomCodes, ct);
    }

    // ── Helper: extract room-type codes from a Method01Detail JSON ────────────

    /// <summary>
    /// Extracts the set of raw room-type codes from a Method01 assumption's DetailJson.
    /// Returns the raw RoomType value (e.g. "01", "99") and falls back to RoomTypeOther
    /// for "other" type rooms. These raw codes are what the FE stores as AnchorRefKey on
    /// RoomIncomeRef analyses — no localization is applied.
    /// Returns an empty set if parsing fails.
    /// </summary>
    public static IReadOnlyCollection<string> ExtractRoomNamesFromMethod01(string detailJson)
    {
        try
        {
            var detail = JsonSerializer.Deserialize<Method01Detail>(detailJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (detail is null) return [];

            return detail.RoomDetails
                // Mirror the FE AnchorRefKey rule: for "other" rooms (code "99") the key is the
                // raw RoomTypeOther text (falling back to "99" when blank); otherwise the raw code.
                .Select(r => r.RoomType == "99"
                    ? (string.IsNullOrWhiteSpace(r.RoomTypeOther) ? "99" : r.RoomTypeOther!)
                    : (r.RoomType ?? r.RoomTypeOther ?? string.Empty))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet();
        }
        catch
        {
            return [];
        }
    }
}
