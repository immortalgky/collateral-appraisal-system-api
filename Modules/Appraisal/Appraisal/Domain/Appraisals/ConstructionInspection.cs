namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Construction inspection record for an appraisal property (1:1).
/// Supports two modes: Full Detail (grouped work items) or Summary (simple overview).
/// Owned by AppraisalProperty via OwnsOne (stored in separate table).
/// </summary>
public class ConstructionInspection : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }
    public bool IsFullDetail { get; private set; }
    public decimal TotalValue { get; private set; }

    // Summary Mode fields (used when IsFullDetail = false)
    public string? SummaryDetail { get; private set; }
    public decimal? SummaryPreviousProgressPct { get; private set; }
    public decimal? SummaryPreviousValue { get; private set; }
    public decimal? SummaryCurrentProgressPct { get; private set; }
    public decimal? SummaryCurrentValue { get; private set; }
    public string? Remark { get; private set; }

    // Document reference (summary mode)
    public Guid? DocumentId { get; private set; }
    public string? FileName { get; private set; }
    public string? FilePath { get; private set; }
    public string? FileExtension { get; private set; }
    public string? MimeType { get; private set; }
    public long? FileSizeBytes { get; private set; }

    // Full Detail Mode - work details
    private readonly List<ConstructionWorkDetail> _workDetails = [];
    public IReadOnlyList<ConstructionWorkDetail> WorkDetails => _workDetails.AsReadOnly();

    private ConstructionInspection()
    {
    }

    /// <summary>
    /// Create a full-detail construction inspection.
    /// </summary>
    public static ConstructionInspection CreateFullDetail(
        Guid appraisalPropertyId,
        decimal totalValue)
    {
        return new ConstructionInspection
        {
            AppraisalPropertyId = appraisalPropertyId,
            IsFullDetail = true,
            TotalValue = totalValue
        };
    }

    /// <summary>
    /// Create a summary-only construction inspection.
    /// </summary>
    public static ConstructionInspection CreateSummary(
        Guid appraisalPropertyId,
        decimal totalValue,
        string? summaryDetail,
        decimal? summaryPreviousProgressPct,
        decimal? summaryPreviousValue,
        decimal? summaryCurrentProgressPct,
        decimal? summaryCurrentValue,
        string? remark)
    {
        return new ConstructionInspection
        {
            AppraisalPropertyId = appraisalPropertyId,
            IsFullDetail = false,
            TotalValue = totalValue,
            SummaryDetail = summaryDetail,
            SummaryPreviousProgressPct = summaryPreviousProgressPct,
            SummaryPreviousValue = summaryPreviousValue,
            SummaryCurrentProgressPct = summaryCurrentProgressPct,
            SummaryCurrentValue = summaryCurrentValue,
            Remark = remark
        };
    }

    /// <summary>
    /// Switch to summary mode. Clears all work details.
    /// </summary>
    public void UpdateSummary(
        decimal totalValue,
        string? summaryDetail,
        decimal? summaryPreviousProgressPct,
        decimal? summaryPreviousValue,
        decimal? summaryCurrentProgressPct,
        decimal? summaryCurrentValue,
        string? remark)
    {
        // Clear work details when switching to summary mode
        if (IsFullDetail)
            _workDetails.Clear();

        IsFullDetail = false;
        TotalValue = totalValue;
        SummaryDetail = summaryDetail;
        SummaryPreviousProgressPct = summaryPreviousProgressPct;
        SummaryPreviousValue = summaryPreviousValue;
        SummaryCurrentProgressPct = summaryCurrentProgressPct;
        SummaryCurrentValue = summaryCurrentValue;
        Remark = remark;
    }

    /// <summary>
    /// Switch to full detail mode. Clears summary fields.
    /// </summary>
    public void UpdateFullDetail(decimal totalValue)
    {
        // Clear summary fields when switching to full detail mode
        if (!IsFullDetail)
        {
            SummaryDetail = null;
            SummaryPreviousProgressPct = null;
            SummaryPreviousValue = null;
            SummaryCurrentProgressPct = null;
            SummaryCurrentValue = null;
            Remark = null;
            ClearDocument();
        }

        IsFullDetail = true;
        TotalValue = totalValue;
    }

    public void SetDocument(
        Guid documentId,
        string? fileName,
        string? filePath,
        string? fileExtension = null,
        string? mimeType = null,
        long? fileSizeBytes = null)
    {
        DocumentId = documentId;
        FileName = fileName;
        FilePath = filePath;
        FileExtension = fileExtension;
        MimeType = mimeType;
        FileSizeBytes = fileSizeBytes;
    }

    public void ClearDocument()
    {
        DocumentId = null;
        FileName = null;
        FilePath = null;
        FileExtension = null;
        MimeType = null;
        FileSizeBytes = null;
    }

    /// <summary>
    /// Add a work detail item (full detail mode).
    /// </summary>
    public ConstructionWorkDetail AddWorkDetail(
        Guid constructionWorkGroupId,
        string workItemName,
        int displayOrder,
        decimal proportionPct,
        decimal previousProgressPct,
        decimal currentProgressPct,
        Guid? constructionWorkItemId = null)
    {
        var detail = ConstructionWorkDetail.Create(
            Id,
            constructionWorkGroupId,
            workItemName,
            displayOrder,
            proportionPct,
            previousProgressPct,
            currentProgressPct,
            constructionWorkItemId);

        _workDetails.Add(detail);
        return detail;
    }

    public void ClearWorkDetails()
    {
        _workDetails.Clear();
    }

    /// <summary>
    /// Compute all derived values for work details.
    /// </summary>
    public void ComputeAllValues()
    {
        foreach (var detail in _workDetails)
        {
            detail.ComputeValues(TotalValue);
        }
    }
}
