using Appraisal.Application.Features.Project.UploadProjectUnits;

namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Re-matches an updated units Excel against the seeded units of a block reappraisal project.
///
/// Match rules (normalized = trim + lowercase):
///   Condo:           CondoRegistrationNumber when non-empty, else (TowerName + "|" + RoomNumber)
///   LandAndBuilding: PlotNumber when non-empty, else HouseNumber
///
/// Actions:
///   key IS in incoming set AND attributes match   → leave IsSold unchanged (already-sold units stay sold)
///   key IS in incoming set BUT attributes differ  → REJECT with BadRequestException (MatchDifference)
///   key NOT in incoming set AND unit is NOT sold  → MarkSoldByReappraisal()
///   key NOT in incoming set AND unit IS sold      → leave as-is (already sold stays sold)
///   incoming row with NO existing match            → counted in Added, NOT persisted in v1
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

        // Build a key map from the existing project units (blank keys excluded) for Added counting.
        var existingByKey = project.Units
            .Select(u => (Unit: u, Key: BlockReappraisalMatcher.BuildKey(u, project.ProjectType)))
            .Where(x => !BlockReappraisalMatcher.IsBlankKey(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Guard: reject if any NOT-sold unit is present in the Excel but has attribute differences.
        // Apply must only flip sold/unsold status — never silently adopt attributes from the Excel.
        int matchDifferenceCount = 0;
        foreach (var existingUnit in project.Units)
        {
            if (existingUnit.IsSold)
                continue; // already-sold units are left untouched regardless

            var key = BlockReappraisalMatcher.BuildKey(existingUnit, project.ProjectType);
            if (BlockReappraisalMatcher.IsBlankKey(key))
                continue;

            if (incomingByKey.TryGetValue(key, out var incomingMatch))
            {
                if (BlockReappraisalMatcher.AttributesDiffer(
                        existingUnit, incomingMatch, project.ProjectType, out _))
                    matchDifferenceCount++;
            }
        }

        if (matchDifferenceCount > 0)
            throw new BadRequestException(
                $"Resolve {matchDifferenceCount} mismatched unit(s) before applying — " +
                "fix the Excel and re-upload, or use the preview endpoint to see which units differ.");

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

        // Count incoming rows that have no existing match (new units in the Excel not yet seeded).
        // v1: these are NOT persisted — callers must use UploadProjectUnits to add new inventory first.
        int added = 0;
        foreach (var incomingUnit in incomingUnits)
        {
            var key = BlockReappraisalMatcher.BuildKey(incomingUnit, project.ProjectType);
            if (BlockReappraisalMatcher.IsBlankKey(key))
                continue;
            if (!existingByKey.ContainsKey(key))
            {
                added++;
                logger.LogInformation(
                    "Block reappraisal re-match: incoming unit with key={Key} has no existing match. " +
                    "Not persisted in v1 — use UploadProjectUnits to add new inventory.",
                    key);
            }
        }

        // Record the revised Excel in Upload History.
        project.RecordReappraisalUpload(command.FileName, command.DocumentId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Block reappraisal re-match complete for appraisal {AppraisalId}: " +
            "Matched={Matched}, AutoSold={AutoSold}, Added(not persisted)={Added}, " +
            "Skipped(no key)={Skipped}.",
            command.AppraisalId, matchedCount, autoSold, added, skipped);

        return new UploadBlockReappraisalUnitsResult(matchedCount, autoSold, added);
    }
}
