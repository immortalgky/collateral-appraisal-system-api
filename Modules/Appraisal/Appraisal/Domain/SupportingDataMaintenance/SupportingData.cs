namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingData : Aggregate<Guid>
{
    public SupportingNumber? SupportingNumber { get; private set; }
    public string? ImportChannel { get; private set; } = default!;
    public DateTime? ImportDate { get; private set; }
    public string? SourceOfData { get; private set; } = default!;
    public Guid? AppraisalCompanyId { get; private set; }
    public string? Description { get; private set; }
    public SupportingStatus Status { get; private set; } = default!;
    public string? Remark { get; private set; }

    private readonly List<SupportingDataDetail> _details = [];
    public IReadOnlyList<SupportingDataDetail> Details => _details.AsReadOnly();


    private SupportingData() { /* EF */ }

    private SupportingData(SupportingDataHeader data)
    {
        Id = Guid.CreateVersion7();
        Status = SupportingStatus.Draft;
        Apply(data);
        AddDomainEvent(new SupportingDataCreatedEvent(Id));
    }

    public static SupportingData Create(SupportingDataHeader data) => new(data);

    public void Update(SupportingDataHeader data) => Apply(data);

    public void Validate()
    {
        var rc = new RuleCheck();
        rc.AddErrorIf(string.IsNullOrWhiteSpace(ImportDate?.ToString()), "ImportDate is required.");
        rc.AddErrorIf(string.IsNullOrWhiteSpace(ImportChannel), "ImportChannel is required.");
        rc.AddErrorIf(string.IsNullOrWhiteSpace(SourceOfData), "SourceOfData is required.");
        rc.ThrowIfInvalid();
    }

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

    /// <summary>
    /// Remove an existing detail row within this supporting data.
    /// </summary>
    public void RemoveDetail(Guid detailId)
    {
        var detail = _details.FirstOrDefault(d => d.Id == detailId)
        ?? throw new SupportingDataDetailNotFoundException(detailId);
        _details.Remove(detail);
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
        ImportChannel = d.ImportChannel;
        ImportDate = d.ImportDate;
        SourceOfData = d.SourceOfData;
        AppraisalCompanyId = d.AppraisalCompanyId;
        Description = d.Description;
        Remark = d.Remark;
    }

    public void Submit(string? decision, string? remark)
    {
        Validate();
        if (Status == SupportingStatus.Draft || Status == SupportingStatus.RoutedBack)
        {
            Status = SupportingStatus.Pending;
            Remark = remark;
        }
        else if (decision == SupportingStatus.Approved)
        {
            Guard(SupportingStatus.Pending);
            Status = SupportingStatus.Approved;
            Remark = remark;
        }
        else if (decision == SupportingStatus.Rejected)
        {
            Guard(SupportingStatus.Pending);
            Status = SupportingStatus.Rejected;
            Remark = remark;
        }
        else if (decision == SupportingStatus.Cancelled)
        {
            Guard(SupportingStatus.Pending);
            Status = SupportingStatus.Cancelled;
            Remark = remark;
        }
        else if (decision == SupportingStatus.RoutedBack)
        {
            Guard(SupportingStatus.Pending);
            Status = SupportingStatus.RoutedBack;
            Remark = remark;
        }
        else
        {
            throw new DomainException($"Invalid decision: {decision}");
        }
    }

    private void Guard(SupportingStatus expected)
    {
        if (Status != expected)
            throw new DomainException($"Invalid transition from {Status}.");
    }
}

public record SupportingDataHeader(
    string? ImportChannel,
    DateTime? ImportDate,
    string? SourceOfData,
    Guid? AppraisalCompanyId,
    string? Description,
    string? Remark
);