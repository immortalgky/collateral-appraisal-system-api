using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.SaveProjectUnitPrices;

/// <summary>
/// Saves location flags for project unit prices.
/// Branches on ProjectType to call the correct domain method:
///   Condo:           UpdateCondoLocationFlags (IsCorner, IsEdge, IsPoolView, IsSouth, IsOther)
///   LandAndBuilding: UpdateLandAndBuildingLocationFlags (IsCorner, IsEdge, IsNearGarden, IsOther)
/// </summary>
public class SaveProjectUnitPricesCommandHandler(
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveProjectUnitPricesCommand>
{
    public async Task<Unit> Handle(
        SaveProjectUnitPricesCommand command,
        CancellationToken cancellationToken)
    {
        var unitIds = command.UnitPriceFlags.Select(f => f.ProjectUnitId).ToList();

        // Resolve ProjectId and ProjectType from the appraisal's project
        var projectInfo = await dbContext.Projects
            .Where(p => p.AppraisalId == command.AppraisalId)
            .Select(p => new { p.Id, p.ProjectType })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // Validate all submitted unit IDs belong to this project
        var validUnitIds = await dbContext.ProjectUnits
            .Where(u => u.ProjectId == projectInfo.Id && unitIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = unitIds.Except(validUnitIds).ToList();
        if (invalidIds.Count > 0)
            throw new BadRequestException(
                "One or more project unit IDs do not belong to this project.",
                $"Invalid IDs: {string.Join(", ", invalidIds)}");

        var existingPrices = await dbContext.ProjectUnitPrices
            .Where(p => unitIds.Contains(p.ProjectUnitId))
            .ToListAsync(cancellationToken);

        var priceMap = existingPrices.ToDictionary(p => p.ProjectUnitId);

        foreach (var flag in command.UnitPriceFlags)
        {
            if (!priceMap.TryGetValue(flag.ProjectUnitId, out var unitPrice))
            {
                unitPrice = ProjectUnitPrice.Create(flag.ProjectUnitId);
                dbContext.ProjectUnitPrices.Add(unitPrice);
            }

            if (projectInfo.ProjectType == ProjectType.Condo)
            {
                unitPrice.UpdateCondoLocationFlags(
                    flag.IsCorner, flag.IsEdge, flag.IsPoolView, flag.IsSouth, flag.IsOther);
            }
            else
            {
                unitPrice.UpdateLandAndBuildingLocationFlags(
                    flag.IsCorner, flag.IsEdge, flag.IsNearGarden, flag.IsOther);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
