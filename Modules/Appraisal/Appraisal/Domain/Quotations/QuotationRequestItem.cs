namespace Appraisal.Domain.Quotations;

/// <summary>
/// Line item linking individual appraisals to an RFQ.
/// </summary>
public class QuotationRequestItem : Entity<Guid>
{
    public Guid QuotationRequestId { get; private set; }
    public Guid AppraisalId { get; private set; }

    // Item Details
    public int ItemNumber { get; private set; }

    // Appraisal Information (denormalized for quick reference)
    public string AppraisalNumber { get; private set; } = null!;
    public string PropertyType { get; private set; } = null!;
    public string? PropertyLocation { get; private set; }
    public decimal? EstimatedValue { get; private set; }

    // Item-Specific Requirements
    public string? ItemNotes { get; private set; }
    public string? SpecialRequirements { get; private set; }

    private QuotationRequestItem()
    {
    }

    public static QuotationRequestItem Create(
        Guid quotationRequestId,
        Guid appraisalId,
        int itemNumber,
        string appraisalNumber,
        string propertyType,
        string? propertyLocation = null,
        decimal? estimatedValue = null)
    {
        return new QuotationRequestItem
        {
            Id = Guid.NewGuid(),
            QuotationRequestId = quotationRequestId,
            AppraisalId = appraisalId,
            ItemNumber = itemNumber,
            AppraisalNumber = appraisalNumber,
            PropertyType = propertyType,
            PropertyLocation = propertyLocation,
            EstimatedValue = estimatedValue
        };
    }

    public void SetNotes(string? notes)
    {
        ItemNotes = notes;
    }

    public void SetSpecialRequirements(string? requirements)
    {
        SpecialRequirements = requirements;
    }
}