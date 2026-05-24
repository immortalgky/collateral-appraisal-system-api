using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;

public class UpdateProjectUnitSaleInfoCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProjectUnitSaleInfoCommand>
{
    public async Task<Unit> Handle(
        UpdateProjectUnitSaleInfoCommand command,
        CancellationToken cancellationToken)
    {
        // GetByIdAsync returns a shallow Project (no units loaded).
        // Resolve the AppraisalId first so we can use GetWithFullGraphAsync to eagerly load units.
        var shallow = await projectRepository.GetByIdAsync(command.ProjectId, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Project {command.ProjectId} not found.");

        // GetWithFullGraphAsync filters by AppraisalId and includes the Units collection.
        var project = await projectRepository.GetWithFullGraphAsync(shallow.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Project {command.ProjectId} not found (full graph).");

        // Build a lookup so we can verify every requested UnitId belongs to this project.
        var unitMap = project.Units.ToDictionary(u => u.Id);

        foreach (var item in command.Items)
        {
            if (!unitMap.TryGetValue(item.UnitId, out var unit))
                throw new InvalidOperationException(
                    $"Unit {item.UnitId} does not belong to project {command.ProjectId}.");

            // Domain method enforces invariants and throws InvalidProjectStateException on violation.
            unit.SetSaleInfo(item.IsSold, item.PurchaseBy, item.LoanBankName);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
