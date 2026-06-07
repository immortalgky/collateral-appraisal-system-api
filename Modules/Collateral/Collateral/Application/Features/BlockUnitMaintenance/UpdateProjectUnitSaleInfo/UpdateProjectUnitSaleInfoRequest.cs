namespace Collateral.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;

/// <summary>HTTP request body for PUT /block-unit-maintenance/{collateralMasterId}/units.</summary>
public record UpdateProjectUnitSaleInfoRequest(IReadOnlyList<UnitSaleInfoItemRequest> Items);

/// <summary>Per-unit payload item.</summary>
public record UnitSaleInfoItemRequest(
    Guid UnitId,
    bool IsSold,
    UnitPurchaseMethod? PurchaseBy,
    string? LoanBankName
);
