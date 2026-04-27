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
