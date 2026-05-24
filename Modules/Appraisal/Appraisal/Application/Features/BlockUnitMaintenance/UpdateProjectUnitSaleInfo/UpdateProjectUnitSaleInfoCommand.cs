using Appraisal.Domain.Projects;

namespace Appraisal.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;

/// <summary>Bulk-updates sale info for one or more units within a project.</summary>
public record UpdateProjectUnitSaleInfoCommand(
    Guid ProjectId,
    IReadOnlyList<UnitSaleInfoItem> Items
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

/// <summary>Per-unit sale info to apply.</summary>
public record UnitSaleInfoItem(
    Guid UnitId,
    bool IsSold,
    UnitPurchaseMethod? PurchaseBy,
    string? LoanBankName
);
