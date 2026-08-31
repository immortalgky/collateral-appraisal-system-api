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
                DiffFields: diffFields.AsReadOnly()));
        }

        var summary = new PreviewSummaryDto(
            Total: unitDtos.Count,
            Sold: sold,
            NewlySold: newlySold,
            Available: available,
            MatchDifference: matchDifference);

        logger.LogInformation(
            "Block reappraisal preview for appraisal {AppraisalId}: " +
            "Total={Total}, Sold={Sold}, NewlySold={NewlySold}, Available={Available}, MatchDifference={MatchDifference}.",
            command.AppraisalId, summary.Total, summary.Sold, summary.NewlySold,
            summary.Available, summary.MatchDifference);

        return new PreviewBlockReappraisalUnitsResult(summary, unitDtos.AsReadOnly());
    }
}
