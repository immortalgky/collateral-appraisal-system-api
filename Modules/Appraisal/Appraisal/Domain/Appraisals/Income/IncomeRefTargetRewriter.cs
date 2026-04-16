using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Microsoft.Extensions.Logging;

namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// Rewrites Method-13 refTarget identifiers so the calculation service can resolve them by dbId.
///
/// Always re-resolves from clientId — under full-replace, the stored dbId is stale
/// the moment a new save starts (every Id rotates).
/// </summary>
public static class IncomeRefTargetRewriter
{
    /// <summary>
    /// Walks every method-13 assumption in <paramref name="analysis"/> and overwrites
    /// <c>refTarget.dbId</c> from <paramref name="idMap"/> using the current save's clientId mapping.
    ///
    /// idMap: clientId (Guid) → dbId (Guid) — built by the caller as each node is created.
    /// </summary>
    public static void Rewrite(
        IncomeAnalysis analysis,
        Dictionary<Guid, Guid> idMap,
        ILogger logger)
    {
        foreach (var section in analysis.Sections)
        foreach (var category in section.Categories)
        foreach (var assumption in category.Assumptions)
        {
            if (assumption.Method.MethodTypeCode != "13")
                continue;

            var detail = MethodDetailSerializer.Deserialize<Method13Detail>(
                "13", assumption.Method.DetailJson);

            var clientIdStr = detail.RefTarget.ClientId;
            if (string.IsNullOrWhiteSpace(clientIdStr))
                continue;

            if (!Guid.TryParse(clientIdStr, out var clientGuid))
            {
                logger.LogDebug(
                    "Method-13 refTarget.clientId '{ClientId}' for assumption {AssumptionId} " +
                    "is not a valid Guid — skipping rewrite.",
                    clientIdStr, assumption.Id);
                continue;
            }

            string? newDbId;
            if (idMap.TryGetValue(clientGuid, out var dbGuid) && dbGuid != Guid.Empty)
            {
                newDbId = dbGuid.ToString();
            }
            else
            {
                // Dangling ref: clear the stale dbId so calc fails fast and the frontend
                // modal shows "Please select" instead of silently producing zeros.
                logger.LogWarning(
                    "Method-13 refTarget.clientId {ClientId} for assumption {AssumptionId} " +
                    "was not found in the current save's idMap — clearing dbId.",
                    clientGuid, assumption.Id);
                newDbId = null;
            }

            var rewritten = detail with
            {
                RefTarget = detail.RefTarget with { DbId = newDbId }
            };

            assumption.Method.SetDetailJson(MethodDetailSerializer.Serialize(rewritten));
        }
    }
}
