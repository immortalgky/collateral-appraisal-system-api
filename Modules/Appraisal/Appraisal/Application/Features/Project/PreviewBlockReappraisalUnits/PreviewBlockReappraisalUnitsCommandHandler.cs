using Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;
using Appraisal.Application.Features.Project.UploadProjectUnits;

namespace Appraisal.Application.Features.Project.PreviewBlockReappraisalUnits;

/// <summary>
/// Dry-run handler: loads the project full graph, parses the Excel, and classifies each
/// existing unit into one of four mutually exclusive status buckets.
/// No SaveChanges is ever called — this is a read + compute only operation.
/// </summary>
public class PreviewBlockReappraisalUnitsCommandHandler(
    IProjectRepository projectRepository,
    ILogger<PreviewBlockReappraisalUnitsCommandHandler> logger)
    : ICommandHandler<PreviewBlockReappraisalUnitsCommand, PreviewBlockReappraisalUnitsResult>
{
    private const int MaxUnits = 10_000;

    public async Task<PreviewBlockReappraisalUnitsResult> Handle(
        PreviewBlockReappraisalUnitsCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Project not found for appraisal {command.AppraisalId}.");

        // Parse the incoming Excel (same parser as the apply handler).
        var incomingUnits = project.ProjectType == ProjectType.Condo
            ? ProjectUnitExcelParser.ParseCondoExcel(command.FileStream, project.Id)
            : ProjectUnitExcelParser.ParseLandAndBuildingExcel(command.FileStream, project.Id);

        if (incomingUnits.Count > MaxUnits)
            throw new BadRequestException(
                $"Too many units. Maximum allowed is {MaxUnits}, but the file contains {incomingUnits.Count}.");

        // Build key→incoming-unit map (blank keys excluded; first occurrence wins on duplicate keys).
        var incomingByKey = incomingUnits
            .Select(u => (Unit: u, Key: BlockReappraisalMatcher.BuildKey(u, project.ProjectType)))
            .Where(x => !BlockReappraisalMatcher.IsBlankKey(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var unitDtos = new List<PreviewUnitDto>(project.Units.Count);

        int sold = 0;
        int newlySold = 0;
        int available = 0;
        int matchDifference = 0;

        foreach (var unit in project.Units.OrderBy(u => u.SequenceNumber))
        {
            string status;
            List<string> diffFields = [];
            Dictionary<string, object?> incomingValues = [];

            if (unit.IsSold)
            {
                // Already sold (carried from master). Apply never touches these.
                status = "Sold";
                sold++;
            }
            else
            {
                var key = BlockReappraisalMatcher.BuildKey(unit, project.ProjectType);

                if (BlockReappraisalMatcher.IsBlankKey(key))
                {
                    // No usable key — treat as Available (cannot be matched or auto-sold).
                    status = "Available";
                    available++;
                    logger.LogDebug(
                        "Preview: unit {UnitId} has no business key; classified as Available.",
                        unit.Id);
                }
                else if (incomingByKey.TryGetValue(key, out var incomingMatch))
                {
                    if (BlockReappraisalMatcher.AttributesDiffer(
                            unit, incomingMatch, project.ProjectType, out diffFields))
                    {
                        status = "MatchDifference";
                        matchDifference++;
                        incomingValues = ReadIncomingValues(incomingMatch, diffFields);
                    }
                    else
                    {
                        status = "Available";
                        available++;
                    }
                }
                else
                {
                    // Key absent from Excel — would be auto-sold on Apply.
                    status = "NewlySold";
                    newlySold++;
                }
            }

            unitDtos.Add(new PreviewUnitDto(
                Id: unit.Id,
                SequenceNumber: unit.SequenceNumber,
                UnitNumber: unit.UnitNumber,
                ModelType: unit.ModelType,
                UsableArea: unit.UsableArea,
                SellingPrice: unit.SellingPrice,
                Floor: unit.Floor,
                TowerName: unit.TowerName,
                CondoRegistrationNumber: unit.CondoRegistrationNumber,
                RoomNumber: unit.RoomNumber,
                PlotNumber: unit.PlotNumber,
                HouseNumber: unit.HouseNumber,
                NumberOfFloors: unit.NumberOfFloors,
                LandArea: unit.LandArea,
                IsSold: unit.IsSold,
                Status: status,
                DiffFields: diffFields.AsReadOnly(),
                IncomingValues: incomingValues));
        }

        // Excel rows that match nothing in the project — what applying the file would ADD.
        // The loop above walks project.Units only, so without this pass a new room is invisible in
        // the preview and then appears out of nowhere on apply.
        var existingKeys = project.Units
            .Select(u => BlockReappraisalMatcher.BuildKey(u, project.ProjectType))
            .Where(k => !BlockReappraisalMatcher.IsBlankKey(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedDtos = incomingUnits
            .Select(u => (Unit: u, Key: BlockReappraisalMatcher.BuildKey(u, project.ProjectType)))
            .Where(x => !BlockReappraisalMatcher.IsBlankKey(x.Key) && !existingKeys.Contains(x.Key))
            .GroupBy(x => x.Key, x => x.Unit, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(u => new PreviewAddedUnitDto(
                ModelType: u.ModelType,
                UsableArea: u.UsableArea,
                SellingPrice: u.SellingPrice,
                Floor: u.Floor,
                TowerName: u.TowerName,
                CondoRegistrationNumber: u.CondoRegistrationNumber,
                RoomNumber: u.RoomNumber,
                PlotNumber: u.PlotNumber,
                HouseNumber: u.HouseNumber,
                NumberOfFloors: u.NumberOfFloors,
                LandArea: u.LandArea))
            .ToList();

        var summary = new PreviewSummaryDto(
            Total: unitDtos.Count,
            Sold: sold,
            NewlySold: newlySold,
            Available: available,
            MatchDifference: matchDifference,
            Added: addedDtos.Count);

        logger.LogInformation(
            "Block reappraisal preview for appraisal {AppraisalId}: " +
            "Total={Total}, Sold={Sold}, NewlySold={NewlySold}, Available={Available}, " +
            "MatchDifference={MatchDifference}, Added={Added}.",
            command.AppraisalId, summary.Total, summary.Sold, summary.NewlySold,
            summary.Available, summary.MatchDifference, summary.Added);

        return new PreviewBlockReappraisalUnitsResult(
            summary, unitDtos.AsReadOnly(), addedDtos.AsReadOnly());
    }

    /// <summary>
    /// Picks out the Excel's value for each field the matcher flagged as different, so the caller
    /// can render old against new. Keyed by the same camelCase names the matcher returns.
    /// </summary>
    private static Dictionary<string, object?> ReadIncomingValues(
        ProjectUnit incoming,
        IReadOnlyList<string> diffFields)
    {
        var values = new Dictionary<string, object?>(diffFields.Count);

        foreach (var field in diffFields)
        {
            values[field] = field switch
            {
                "modelType" => incoming.ModelType,
                "sellingPrice" => incoming.SellingPrice,
                "usableArea" => incoming.UsableArea,
                "floor" => incoming.Floor,
                "numberOfFloors" => incoming.NumberOfFloors,
                "landArea" => incoming.LandArea,
                _ => null
            };
        }

        return values;
    }
}
