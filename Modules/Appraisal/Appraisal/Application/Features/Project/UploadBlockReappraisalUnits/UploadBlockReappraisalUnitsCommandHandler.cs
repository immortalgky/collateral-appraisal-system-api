using Appraisal.Application.Features.Project.UploadProjectUnits;

namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Re-matches an updated units Excel against the units of a block reappraisal project.
///
/// Match rules (normalized = trim + lowercase):
///   Condo:           CondoRegistrationNumber when non-empty, else (TowerName + "|" + RoomNumber)
///   LandAndBuilding: PlotNumber when non-empty, else HouseNumber
///
/// Actions:
///   key IS in incoming set                         → confirmed still unsold (already-sold units stay sold)
///   key IS in incoming set, attributes differ      → attributes refreshed, but only with ConfirmUpdates
///   key NOT in incoming set AND unit is NOT sold   → MarkSoldByReappraisal()
///   key NOT in incoming set AND unit IS sold       → leave as-is
///   incoming row with NO existing match            → appended, but only with ConfirmUpdates
///   blank key                                      → skipped, never auto-sold
///
/// Why ConfirmUpdates gates the last two. A reappraisal's units are seeded from the collateral
/// master and carry sale state the workbook knows nothing about, so this endpoint reconciles rather
/// than replaces — which also means a stale or wrong workbook reaching it could rewrite prices or
/// add rooms that do not exist, with nothing to undo it. Attribute differences have always been
/// refused here; the flag turns that blanket refusal into a decision the caller makes after reading
/// the preview, and extends the same protection to additions. Sold/unsold reconciliation is the
/// endpoint's stated purpose and stays unconditional.
/// </summary>
public class UploadBlockReappraisalUnitsCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalUnitOfWork unitOfWork,
    ILogger<UploadBlockReappraisalUnitsCommandHandler> logger)
    : ICommandHandler<UploadBlockReappraisalUnitsCommand, UploadBlockReappraisalUnitsResult>
{
    private const int MaxUnits = 10_000;

    public async Task<UploadBlockReappraisalUnitsResult> Handle(
        UploadBlockReappraisalUnitsCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Project not found for appraisal {command.AppraisalId}.");

        // Parse the incoming Excel using the shared parser (same columns as UploadProjectUnits).
        var incomingUnits = project.ProjectType == ProjectType.Condo
            ? ProjectUnitExcelParser.ParseCondoExcel(command.FileStream, project.Id)
            : ProjectUnitExcelParser.ParseLandAndBuildingExcel(command.FileStream, project.Id);

        if (incomingUnits.Count > MaxUnits)
            throw new BadRequestException(
                $"Too many units. Maximum allowed is {MaxUnits}, but the file contains {incomingUnits.Count}.");

        // Build a key→incoming-unit map (blank keys excluded).
        var incomingByKey = incomingUnits
            .Select(u => (Unit: u, Key: BlockReappraisalMatcher.BuildKey(u, project.ProjectType)))
            .Where(x => !BlockReappraisalMatcher.IsBlankKey(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Build a key map from the existing project units (blank keys excluded).
        var existingByKey = project.Units
            .Select(u => (Unit: u, Key: BlockReappraisalMatcher.BuildKey(u, project.ProjectType)))
            .Where(x => !BlockReappraisalMatcher.IsBlankKey(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Existing not-sold units whose attributes disagree with the Excel.
        var differing = new List<(ProjectUnit Existing, ProjectUnit Incoming)>();
        foreach (var existingUnit in project.Units)
        {
            if (existingUnit.IsSold)
                continue; // already-sold units are left untouched regardless

            var key = BlockReappraisalMatcher.BuildKey(existingUnit, project.ProjectType);
            if (BlockReappraisalMatcher.IsBlankKey(key))
                continue;

            if (incomingByKey.TryGetValue(key, out var incomingMatch)
                && BlockReappraisalMatcher.AttributesDiffer(
                    existingUnit, incomingMatch, project.ProjectType, out _))
            {
                differing.Add((existingUnit, incomingMatch));
            }
        }

        // Incoming rows that match nothing in the project — new inventory.
        var newUnits = incomingUnits
            .Select(u => (Unit: u, Key: BlockReappraisalMatcher.BuildKey(u, project.ProjectType)))
            .Where(x => !BlockReappraisalMatcher.IsBlankKey(x.Key) && !existingByKey.ContainsKey(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (!command.ConfirmUpdates && (differing.Count > 0 || newUnits.Count > 0))
            throw new BadRequestException(
                $"This file would change {differing.Count} existing unit(s) and add {newUnits.Count} " +
                "new one(s). Review them with the preview endpoint, then re-send with confirmUpdates=true.");

        int matchedCount = 0;
        int autoSold = 0;
        int skipped = 0;

        foreach (var existingUnit in project.Units)
        {
            var key = BlockReappraisalMatcher.BuildKey(existingUnit, project.ProjectType);

            // Units without a usable business key cannot be safely matched — never auto-sell them.
            if (BlockReappraisalMatcher.IsBlankKey(key))
            {
                skipped++;
                logger.LogWarning(
                    "Block reappraisal re-match: unit {UnitId} has no usable business key; left unchanged.",
                    existingUnit.Id);
                continue;
            }

            if (incomingByKey.ContainsKey(key))
            {
                // Unit is present in the new Excel.
                // Already-sold units stay sold — BUM edits the master; we don't reset sold status.
                matchedCount++;
            }
            else
            {
                // Unit is absent from the new Excel.
                // Only flip NOT-sold units to sold; already-sold units stay sold.
                if (!existingUnit.IsSold)
                {
                    existingUnit.MarkSoldByReappraisal();
                    autoSold++;
                    logger.LogInformation(
                        "Block reappraisal re-match: unit {UnitId} (key={Key}) absent from Excel; auto-marked as sold.",
                        existingUnit.Id, key);
                }
            }
        }

        // Attribute refresh. Only the fields BlockReappraisalMatcher compares are written — identity
        // fields are the matching key and must never move.
        //
        // SellingPrice / UsableArea / LandArea feed Project.CalculateUnitPrices, so any already
        // calculated ProjectUnitPrice for these units is now out of date. The prices are left alone
        // rather than recalculated: an appraiser may have adjusted them by hand, and overwriting
        // that silently is worse than showing a stale figure. The Updated count is returned so the
        // caller can prompt for a recalculation.
        foreach (var (existing, incoming) in differing)
        {
            existing.UpdateAttributesFrom(incoming, project.ProjectType);
            logger.LogInformation(
                "Block reappraisal re-match: refreshed attributes of unit {UnitId} from the Excel.",
                existing.Id);
        }

        // Record the revised Excel in Upload History, appending any new inventory to the same batch.
        project.RecordReappraisalUpload(
            command.FileName, command.DocumentId, newUnits,
            matchedUnsold: matchedCount, autoSold: autoSold, updated: differing.Count);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Block reappraisal re-match complete for appraisal {AppraisalId}: " +
            "Matched={Matched}, AutoSold={AutoSold}, Added={Added}, Updated={Updated}, " +
            "Skipped(no key)={Skipped}.",
            command.AppraisalId, matchedCount, autoSold, newUnits.Count, differing.Count, skipped);

        return new UploadBlockReappraisalUnitsResult(
            matchedCount, autoSold, newUnits.Count, differing.Count);
    }
}
