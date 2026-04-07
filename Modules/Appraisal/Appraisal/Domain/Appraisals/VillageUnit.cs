namespace Appraisal.Domain.Appraisals;

public class VillageUnit : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public Guid UploadBatchId { get; private set; }
    public Guid? VillageModelId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string? PlotNumber { get; private set; }
    public string? HouseNumber { get; private set; }
    public string? ModelName { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public decimal? LandArea { get; private set; }
    public decimal? UsableArea { get; private set; }
    public decimal? SellingPrice { get; private set; }

    private VillageUnit()
    {
    }

    public static VillageUnit Create(
        Guid appraisalId,
        Guid uploadBatchId,
        int sequenceNumber,
        string? plotNumber = null,
        string? houseNumber = null,
        string? modelName = null,
        int? numberOfFloors = null,
        decimal? landArea = null,
        decimal? usableArea = null,
        decimal? sellingPrice = null)
    {
        return new VillageUnit
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            UploadBatchId = uploadBatchId,
            SequenceNumber = sequenceNumber,
            PlotNumber = plotNumber,
            HouseNumber = houseNumber,
            ModelName = modelName,
            NumberOfFloors = numberOfFloors,
            LandArea = landArea,
            UsableArea = usableArea,
            SellingPrice = sellingPrice
        };
    }

    /// <summary>
    /// Links this unit to the actual VillageUnitUpload entity.
    /// Called by the aggregate root after creating the upload record.
    /// </summary>
    internal void SetUploadBatchId(Guid uploadBatchId)
    {
        UploadBatchId = uploadBatchId;
    }

    internal void SetVillageModelId(Guid modelId)
    {
        VillageModelId = modelId;
    }
}
