namespace Collateral.CollateralMasters.Models;

/// <summary>
/// First-class per-unit master record for a PRJ (block-project) <see cref="CollateralMaster"/>.
/// Replaces the opaque <c>StructureJson</c> blob that existed on <see cref="ProjectDetail"/>.
///
/// Condo-type fields (Floor, TowerName, CondoRegistrationNumber, RoomNumber) and
/// LandAndBuilding-type fields (PlotNumber, HouseNumber, NumberOfFloors, LandArea) are both
/// nullable — only the relevant set is populated depending on the parent project's
/// <see cref="ProjectDetail.ProjectType"/>.
///
/// Phase 1: entity + schema only. Population from appraisals is wired in Phase 2.
/// </summary>
public class ProjectUnit : Entity<Guid>
{
    /// <summary>FK to the parent <see cref="ProjectDetail"/> / <see cref="CollateralMaster"/>.</summary>
    public Guid CollateralMasterId { get; private set; }

    /// <summary>1-based display order within the project.</summary>
    public int SequenceNumber { get; private set; }

    // ----- Condo-side identity fields (nullable; populated only for Condo projects) -----

    public int? Floor { get; private set; }
    public string? TowerName { get; private set; }
    public string? CondoRegistrationNumber { get; private set; }
    public string? RoomNumber { get; private set; }

    // ----- LandAndBuilding-side identity fields (nullable; populated only for L&B projects) -----

    public string? PlotNumber { get; private set; }
    public string? HouseNumber { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public decimal? LandArea { get; private set; }

    // ----- Common fields -----

    public string? ModelType { get; private set; }
    public decimal? UsableArea { get; private set; }
    public decimal? SellingPrice { get; private set; }

    // ----- Sale-tracking fields -----

    /// <summary>Whether this unit has been sold. Defaults to false.</summary>
    public bool IsSold { get; private set; }

    /// <summary>How the buyer financed the purchase. Null when the unit is not sold.</summary>
    public UnitPurchaseMethod? PurchaseBy { get; private set; }

    /// <summary>
    /// Name of the financing bank.
    /// Required when <see cref="PurchaseBy"/> is <see cref="UnitPurchaseMethod.Loan"/>; null otherwise.
    /// </summary>
    public string? LoanBankName { get; private set; }

    // ----- Reference value (read-only, set by Phase 2 upsert) -----

    /// <summary>
    /// Last appraised value for this unit, carried over from the most-recent appraisal.
    /// Null until at least one appraisal has completed (Phase 2 populates this field).
    /// </summary>
    public decimal? LastAppraisedValue { get; private set; }

    private ProjectUnit() { }

    /// <summary>Creates a Condo unit record for a PRJ master.</summary>
    public static ProjectUnit CreateCondo(
        Guid collateralMasterId,
        int sequenceNumber,
        int? floor = null,
        string? towerName = null,
        string? condoRegistrationNumber = null,
        string? roomNumber = null,
        string? modelType = null,
        decimal? usableArea = null,
        decimal? sellingPrice = null)
    {
        return new ProjectUnit
        {
            Id = Guid.CreateVersion7(),
            CollateralMasterId = collateralMasterId,
            SequenceNumber = sequenceNumber,
            Floor = floor,
            TowerName = towerName,
            CondoRegistrationNumber = condoRegistrationNumber,
            RoomNumber = roomNumber,
            ModelType = modelType,
            UsableArea = usableArea,
            SellingPrice = sellingPrice,
            IsSold = false
        };
    }

    /// <summary>Creates a LandAndBuilding unit record for a PRJ master.</summary>
    public static ProjectUnit CreateLandAndBuilding(
        Guid collateralMasterId,
        int sequenceNumber,
        string? plotNumber = null,
        string? houseNumber = null,
        string? modelType = null,
        int? numberOfFloors = null,
        decimal? landArea = null,
        decimal? usableArea = null,
        decimal? sellingPrice = null)
    {
        return new ProjectUnit
        {
            Id = Guid.CreateVersion7(),
            CollateralMasterId = collateralMasterId,
            SequenceNumber = sequenceNumber,
            PlotNumber = plotNumber,
            HouseNumber = houseNumber,
            ModelType = modelType,
            NumberOfFloors = numberOfFloors,
            LandArea = landArea,
            UsableArea = usableArea,
            SellingPrice = sellingPrice,
            IsSold = false
        };
    }

    /// <summary>
    /// Updates sale-tracking fields.
    /// </summary>
    /// <remarks>
    /// Invariants:
    /// <list type="bullet">
    ///   <item>When <paramref name="isSold"/> is <c>false</c>, purchase method and bank name are forced null.</item>
    ///   <item>When <paramref name="isSold"/> is <c>true</c>, <paramref name="purchaseBy"/> must be provided.</item>
    ///   <item>When <paramref name="purchaseBy"/> is <see cref="UnitPurchaseMethod.Loan"/>,
    ///     <paramref name="loanBankName"/> must be non-empty.</item>
    ///   <item>When <paramref name="purchaseBy"/> is <see cref="UnitPurchaseMethod.Cash"/>,
    ///     <paramref name="loanBankName"/> is forced null.</item>
    /// </list>
    /// </remarks>
    public void SetSaleInfo(bool isSold, UnitPurchaseMethod? purchaseBy, string? loanBankName)
    {
        if (!isSold)
        {
            IsSold = false;
            PurchaseBy = null;
            LoanBankName = null;
            return;
        }

        if (purchaseBy is null)
            throw new DomainException("PurchaseBy is required when the unit is sold.");

        if (purchaseBy == UnitPurchaseMethod.Loan)
        {
            if (string.IsNullOrWhiteSpace(loanBankName))
                throw new DomainException("LoanBankName is required when PurchaseBy is Loan.");

            IsSold = true;
            PurchaseBy = UnitPurchaseMethod.Loan;
            LoanBankName = loanBankName.Trim();
        }
        else
        {
            IsSold = true;
            PurchaseBy = purchaseBy;
            LoanBankName = null;
        }
    }

    /// <summary>
    /// Convenience: marks unit as sold without a known purchase method.
    /// Used by system-driven block-reappraisal Excel re-match when a prior unit is absent
    /// from the new Excel (treated as sold; user corrects via BUM screen in Phase 2).
    /// Mirrors <c>Appraisal.Domain.Projects.ProjectUnit.MarkSoldByReappraisal()</c>.
    /// </summary>
    public void MarkSold()
    {
        IsSold = true;
        // PurchaseBy and LoanBankName intentionally left as-is (or null for new units).
        // Caller may invoke SetSaleInfo later with full details.
    }

    /// <summary>
    /// Sets the last appraised value reference. Called by Phase 2 upsert logic.
    /// </summary>
    internal void SetLastAppraisedValue(decimal? value)
    {
        LastAppraisedValue = value;
    }
}
