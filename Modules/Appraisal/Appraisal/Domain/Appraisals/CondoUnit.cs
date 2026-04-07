namespace Appraisal.Domain.Appraisals;

public class CondoUnit : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public Guid UploadBatchId { get; private set; }
    public Guid? CondoTowerId { get; private set; }
    public Guid? CondoModelId { get; private set; }
    public int SequenceNumber { get; private set; }
    public int? Floor { get; private set; }
    public string? TowerName { get; private set; }
    public string? CondoRegistrationNumber { get; private set; }
    public string? RoomNumber { get; private set; }
    public string? ModelType { get; private set; }
    public decimal? UsableArea { get; private set; }
    public decimal? SellingPrice { get; private set; }

    private CondoUnit()
    {
    }

    public static CondoUnit Create(
        Guid appraisalId,
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
        return new CondoUnit
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
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

    /// <summary>
    /// Links this unit to the actual CondoUnitUpload entity.
    /// Called by the aggregate root after creating the upload record.
    /// </summary>
    internal void SetUploadBatchId(Guid uploadBatchId)
    {
        UploadBatchId = uploadBatchId;
    }

    internal void SetCondoTowerId(Guid towerId)
    {
        CondoTowerId = towerId;
    }

    internal void SetCondoModelId(Guid modelId)
    {
        CondoModelId = modelId;
    }
}
