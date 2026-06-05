using Appraisal.Application.Features.Project.UploadProjectUnits;

namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Re-matches an updated units Excel against the seeded units of a block reappraisal project.
///
/// Match rules (normalized = trim + lowercase):
///   Condo:          CondoRegistrationNumber when non-empty, else (TowerName + "|" + RoomNumber)
///   LandAndBuilding: PlotNumber when non-empty, else HouseNumber
///
/// Actions:
///   key IS in incoming set  → ensure IsSold=false (unit is still available)
///   key NOT in incoming set → MarkSoldByReappraisal() (system marks as sold, PurchaseBy=null)
///   incoming row with NO existing match → counted in Added, NOT persisted in v1
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

        // Build a key set from the incoming Excel rows — blank keys are excluded so units
        // with no usable identity never collapse into one bucket and mis-match each other.
        var incomingKeys = new HashSet<string>(
            incomingUnits.Select(u => BuildKey(u, project.ProjectType)).Where(k => !IsBlankKey(k)),
            StringComparer.OrdinalIgnoreCase);

        // Build a key map from the existing project units (blank keys excluded) for fast lookup.
        var existingByKey = project.Units
            .Select(u => (Unit: u, Key: BuildKey(u, project.ProjectType)))
            .Where(x => !IsBlankKey(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        int matchedUnsold = 0;
        int autoSold = 0;
        int skipped = 0;

        foreach (var existingUnit in project.Units)
        {
            var key = BuildKey(existingUnit, project.ProjectType);

            // Units without a usable business key cannot be safely matched — never auto-sell them.
            if (IsBlankKey(key))
            {
                skipped++;
                logger.LogWarning(
                    "Block reappraisal re-match: unit {UnitId} has no usable business key; left unchanged.",
                    existingUnit.Id);
                continue;
            }

            if (incomingKeys.Contains(key))
            {
                // Unit is present in the new Excel — it is still unsold.
                // SetSaleInfo(false, null, null) resets IsSold without the PurchaseBy invariant.
                if (existingUnit.IsSold)
                {
                    existingUnit.SetSaleInfo(false, null, null);
                    logger.LogInformation(
                        "Block reappraisal re-match: unit {UnitId} (key={Key}) was sold but reappeared in Excel; reset to unsold.",
                        existingUnit.Id, key);
                }
                matchedUnsold++;
            }
            else
            {
                // Unit is absent from the new Excel — treat as sold (purchase method unknown).
                // Count only units newly marked sold by this operation, so AutoSold matches its
                // documented meaning (units already sold beforehand are not re-counted).
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
            var key = BuildKey(incomingUnit, project.ProjectType);
            if (IsBlankKey(key))
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

        // Record the revised Excel in Upload History (re-match does not replace units, so this is
        // the only place the file gets logged). Marked as the current "Used" upload.
        project.RecordReappraisalUpload(command.FileName, command.DocumentId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Block reappraisal re-match complete for appraisal {AppraisalId}: " +
            "MatchedUnsold={MatchedUnsold}, AutoSold={AutoSold}, Added(not persisted)={Added}, " +
            "Skipped(no key)={Skipped}.",
            command.AppraisalId, matchedUnsold, autoSold, added, skipped);

        return new UploadBlockReappraisalUnitsResult(matchedUnsold, autoSold, added);
    }

    /// <summary>
    /// Builds a normalized business key for matching existing and incoming units.
    ///
    /// Condo:  CondoRegistrationNumber (trimmed, lowercased) when non-empty;
    ///         otherwise (TowerName|RoomNumber) composite — handles units with no reg number yet.
    ///
    /// L&amp;B:  PlotNumber (trimmed, lowercased) when non-empty;
    ///         otherwise HouseNumber — handles units where PlotNumber is absent.
    /// </summary>
    private static string BuildKey(ProjectUnit unit, ProjectType projectType)
    {
        if (projectType == ProjectType.Condo)
        {
            var reg = unit.CondoRegistrationNumber?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(reg))
                return reg;

            var tower = unit.TowerName?.Trim().ToLowerInvariant() ?? string.Empty;
            var room = unit.RoomNumber?.Trim().ToLowerInvariant() ?? string.Empty;
            return $"{tower}|{room}";
        }
        else
        {
            var plot = unit.PlotNumber?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(plot))
                return plot;

            return unit.HouseNumber?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }

    /// <summary>
    /// A key is "blank" (no usable identity) when it is empty/whitespace or the empty Condo
    /// composite "|". Such units cannot be safely matched and must never be auto-sold.
    /// </summary>
    private static bool IsBlankKey(string key) =>
        string.IsNullOrWhiteSpace(key) || key == "|";
}
