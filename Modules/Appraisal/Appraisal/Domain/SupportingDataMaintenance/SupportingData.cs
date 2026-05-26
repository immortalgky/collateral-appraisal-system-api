namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingData : Aggregate<Guid>
{
    public SupportingNumber? SupportingNumber { get; private set; }
    public string ImportChannel { get; private set; } = default!;
    public DateTime ImportDate { get; private set; }
    public string SourceOfData { get; private set; } = default!;
    public string? AppraisalCompany { get; private set; }
    public string? Description { get; private set; }
    public SupportingStatus Status { get; private set; } = default!;
    public string? Remark { get; private set; }

    private readonly List<SupportingDataDetail> _details = [];
    public IReadOnlyList<SupportingDataDetail> Details => _details.AsReadOnly();

    private SupportingData() { /* EF */ }

    private SupportingData(SupportingDataHeader data)
    {
        Id = Guid.CreateVersion7();
        Status = SupportingStatus.PendingApproval;
        SupportingNumber = SupportingNumber.Create($"SUP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Id.ToString()[..8].ToUpper()}");
        Apply(data);
        AddDomainEvent(new SupportingDataCreatedEvent(Id));
    }

    public static SupportingData Create(SupportingDataHeader data) => new(data);

    public void Update(SupportingDataHeader data) => Apply(data);

    /// <summary>
    /// Adds a new detail row to this supporting data.
    /// </summary>
    public SupportingDataDetail AddDetail(SupportingDataDetailData data)
    {
        var detail = SupportingDataDetail.Create(Id, data);
        _details.Add(detail);
        return detail;
    }

    /// <summary>
    /// Updates an existing detail row within this supporting data.
    /// </summary>
    public void UpdateDetail(Guid detailId, SupportingDataDetailData data)
    {
        var detail = _details.FirstOrDefault(d => d.Id == detailId)
            ?? throw new SupportingDataDetailNotFoundException(detailId);
        detail.Update(data);
    }

    public SupportingDataDetail? GetDetail(Guid detailId)
        => _details.FirstOrDefault(d => d.Id == detailId);

    internal void SetSupportingNumber(SupportingNumber n)
    {
        ArgumentNullException.ThrowIfNull(n);
        SupportingNumber = n;
    }

    private void Apply(SupportingDataHeader d)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(d.ImportChannel);
        ArgumentException.ThrowIfNullOrWhiteSpace(d.SourceOfData);

        ImportChannel = d.ImportChannel;
        ImportDate = d.ImportDate;
        SourceOfData = d.SourceOfData;
        AppraisalCompany = d.AppraisalCompany;
        Description = d.Description;
        Remark = d.Remark;
    }
}

public record SupportingDataHeader(
    string ImportChannel,
    DateTime ImportDate,
    string SourceOfData,
    string? AppraisalCompany,
    string? Description,
    string? Remark
);