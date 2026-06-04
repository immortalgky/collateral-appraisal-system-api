using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Domain.Projects;

/// <summary>
/// Superset of CondoUnit + VillageUnit.
/// Condo-side fields (Floor, TowerName, RoomNumber, CondoRegistrationNumber, ProjectTowerId) are
/// nullable and only populated for Condo projects.
/// LB-side fields (PlotNumber, HouseNumber, NumberOfFloors, LandArea) are nullable and only
/// populated for LandAndBuilding projects.
/// </summary>
public class ProjectUnit : Entity<Guid>
{
    public Guid ProjectId { get; private set; }
    public Guid UploadBatchId { get; private set; }

    // Optional links to tower and model
    public Guid? ProjectTowerId { get; private set; }   // Condo only
    public Guid? ProjectModelId { get; private set; }

    public int SequenceNumber { get; private set; }

    // ----- Common Fields -----
    public string? ModelType { get; private set; }
    public decimal? UsableArea { get; private set; }
    public decimal? SellingPrice { get; private set; }

    // ----- Condo-Side Fields (nullable) -----
    public int? Floor { get; private set; }
    public string? TowerName { get; private set; }
    public string? CondoRegistrationNumber { get; private set; }
    public string? RoomNumber { get; private set; }

    // ----- LandAndBuilding-Side Fields (nullable) -----
    public string? PlotNumber { get; private set; }
    public string? HouseNumber { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public decimal? LandArea { get; private set; }

    // ----- Sale Tracking Fields -----

    /// <summary>Whether this unit has been sold. Defaults to false.</summary>
    public bool IsSold { get; private set; }

    /// <summary>How the buyer financed the purchase. Null when the unit is not sold.</summary>
    public UnitPurchaseMethod? PurchaseBy { get; private set; }

    /// <summary>
    /// Name of the financing bank. Required when <see cref="PurchaseBy"/> is
    /// <see cref="UnitPurchaseMethod.Loan"/>; null otherwise.
    /// </summary>
    public string? LoanBankName { get; private set; }

    private ProjectUnit()
    {
    }

    /// <summary>Creates a Condo unit from upload CSV row.</summary>
    public static ProjectUnit CreateCondo(
        Guid projectId,
        Guid uploadBatchId,
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
            ProjectId = projectId,
            UploadBatchId = uploadBatchId,
            SequenceNumber = sequenceNumber,
            Floor = floor,
            TowerName = towerName,
            CondoRegistrationNumber = condoRegistrationNumber,
            RoomNumber = roomNumber,
            ModelType = modelType,
            UsableArea = usableArea,
            SellingPrice = sellingPrice
        };
    }

    /// <summary>Creates a LandAndBuilding unit from upload CSV row.</summary>
    public static ProjectUnit CreateLandAndBuilding(
        Guid projectId,
        Guid uploadBatchId,
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
            ProjectId = projectId,
            UploadBatchId = uploadBatchId,
            SequenceNumber = sequenceNumber,
            PlotNumber = plotNumber,
            HouseNumber = houseNumber,
            ModelType = modelType,
            NumberOfFloors = numberOfFloors,
            LandArea = landArea,
            UsableArea = usableArea,
            SellingPrice = sellingPrice
        };
    }

    /// <summary>
    /// Updates the sale-tracking fields for this unit.
    /// </summary>
    /// <remarks>
    /// Invariants:
    /// <list type="bullet">
    ///   <item>When <paramref name="isSold"/> is <c>false</c>, <paramref name="purchaseBy"/>
    ///     and <paramref name="loanBankName"/> are forced to <c>null</c>.</item>
    ///   <item>When <paramref name="isSold"/> is <c>true</c>, <paramref name="purchaseBy"/>
    ///     must be provided.</item>
    ///   <item>When <paramref name="purchaseBy"/> is <see cref="UnitPurchaseMethod.Loan"/>,
    ///     <paramref name="loanBankName"/> must be non-empty.</item>
    ///   <item>When <paramref name="purchaseBy"/> is <see cref="UnitPurchaseMethod.Cash"/>,
    ///     <paramref name="loanBankName"/> is forced to <c>null</c>.</item>
    /// </list>
    /// </remarks>
    internal void SetSaleInfo(bool isSold, UnitPurchaseMethod? purchaseBy, string? loanBankName)
    {
        if (!isSold)
        {
            IsSold = false;
            PurchaseBy = null;
            LoanBankName = null;
            return;
        }

        // isSold == true from here
        if (purchaseBy is null)
            throw new InvalidProjectStateException(
                "PurchaseBy is required when the unit is sold.");

        if (purchaseBy == UnitPurchaseMethod.Loan)
        {
            if (string.IsNullOrWhiteSpace(loanBankName))
                throw new InvalidProjectStateException(
                    "LoanBankName is required when PurchaseBy is Loan.");

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
    /// System-driven sold mark for block reappraisal Excel re-match: a prior unit absent from the
    /// new Excel is treated as sold, but the purchase method is unknown.
    /// Bypasses the Cash/Loan invariant of <see cref="SetSaleInfo"/> on purpose — the user can
    /// later correct PurchaseBy via the Block Unit Maintenance screen.
    /// </summary>
    public void MarkSoldByReappraisal()
    {
        IsSold = true;
        PurchaseBy = null;
        LoanBankName = null;
    }

    internal void SetUploadBatchId(Guid uploadBatchId)
    {
        UploadBatchId = uploadBatchId;
    }

    internal void SetProjectTowerId(Guid towerId)
    {
        ProjectTowerId = towerId;
    }

    internal void SetProjectModelId(Guid modelId)
    {
        ProjectModelId = modelId;
    }
}
