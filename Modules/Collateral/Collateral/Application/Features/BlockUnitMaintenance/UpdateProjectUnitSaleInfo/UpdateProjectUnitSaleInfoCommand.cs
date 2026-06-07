namespace Collateral.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;

/// <summary>Bulk-updates sale info for one or more units within a collateral project.</summary>
public record UpdateProjectUnitSaleInfoCommand(
    Guid CollateralMasterId,
    IReadOnlyList<UnitSaleInfoItem> Items
) : ICommand;

/// <summary>Per-unit sale info to apply.</summary>
public record UnitSaleInfoItem(
    Guid UnitId,
    bool IsSold,
    UnitPurchaseMethod? PurchaseBy,
    string? LoanBankName
);
