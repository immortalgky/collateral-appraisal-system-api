namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Market Comparable Aggregate Root.
/// Centralized bank-wide database of verified property transactions.
/// </summary>
public class MarketComparable : Aggregate<Guid>
{
    // Core Properties
    public string ComparableNumber { get; private set; } = null!;
    public string PropertyType { get; private set; } = null!; // Land, Building, Condo, etc.

    // Location
    public string Province { get; private set; } = null!;
    public string? District { get; private set; }
    public string? SubDistrict { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Transaction Data
    public string? TransactionType { get; private set; } // Sale, Listing, Auction
    public DateTime? TransactionDate { get; private set; }
    public decimal? TransactionPrice { get; private set; }
    public decimal? PricePerUnit { get; private set; }
    public string? UnitType { get; private set; } // Sqm, Rai, Unit

    // Data Quality
    public string DataSource { get; private set; } = null!; // LandOffice, Survey, Listing, Internal
    public string? DataConfidence { get; private set; } // High, Medium, Low
    public bool IsVerified { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public Guid? VerifiedBy { get; private set; }

    // Status
    public string Status { get; private set; } = "Active"; // Active, Expired, Flagged
    public DateTime? ExpiryDate { get; private set; }

    // Survey Info
    public DateTime SurveyDate { get; private set; }
    public Guid? SurveyedBy { get; private set; }

    // Notes
    public string? Description { get; private set; }
    public string? Notes { get; private set; }

    // Template Reference (optional - tracks which template was used)
    public Guid? TemplateId { get; private set; }

    // Soft Delete
    public SoftDelete SoftDelete { get; private set; } = SoftDelete.NotDeleted();

    // Child Collections (EAV Data and Images)
    private readonly List<MarketComparableData> _factorData = [];
    private readonly List<MarketComparableImage> _images = [];

    public IReadOnlyList<MarketComparableData> FactorData => _factorData.AsReadOnly();
    public IReadOnlyList<MarketComparableImage> Images => _images.AsReadOnly();

    private MarketComparable()
    {
    }

    public static MarketComparable Create(
        string comparableNumber,
        string propertyType,
        string province,
        string dataSource,
        DateTime surveyDate,
        Guid? templateId = null)
    {
        return new MarketComparable
        {
            Id = Guid.NewGuid(),
            ComparableNumber = comparableNumber,
            PropertyType = propertyType,
            Province = province,
            DataSource = dataSource,
            SurveyDate = surveyDate,
            Status = "Active",
            TemplateId = templateId
        };
    }

    public void SetLocation(string? district, string? subDistrict, string? address, decimal? lat, decimal? lng)
    {
        District = district;
        SubDistrict = subDistrict;
        Address = address;
        Latitude = lat;
        Longitude = lng;
    }

    public void SetTransaction(string? type, DateTime? date, decimal? price, decimal? pricePerUnit, string? unitType)
    {
        TransactionType = type;
        TransactionDate = date;
        TransactionPrice = price;
        PricePerUnit = pricePerUnit;
        UnitType = unitType;
    }

    public void Verify(Guid verifiedBy)
    {
        IsVerified = true;
        VerifiedAt = DateTime.UtcNow;
        VerifiedBy = verifiedBy;
        DataConfidence = "High";
    }

    public void Flag(string reason)
    {
        Status = "Flagged";
        Notes = reason;
    }

    public void Expire()
    {
        Status = "Expired";
        ExpiryDate = DateTime.UtcNow;
    }

    public void Delete(Guid deletedBy)
    {
        SoftDelete = SoftDelete.Deleted(deletedBy);
    }

    public void SetTemplate(Guid templateId)
    {
        TemplateId = templateId;
    }

    // Factor Data Management
    public MarketComparableData SetFactorValue(Guid factorId, string? value, string? otherRemarks = null)
    {
        var existing = _factorData.FirstOrDefault(d => d.FactorId == factorId);
        if (existing != null)
        {
            existing.UpdateValue(value, otherRemarks);
            return existing;
        }

        var data = MarketComparableData.Create(Id, factorId, value, otherRemarks);
        _factorData.Add(data);
        return data;
    }

    public void RemoveFactorValue(Guid factorId)
    {
        var data = _factorData.FirstOrDefault(d => d.FactorId == factorId);
        if (data != null)
            _factorData.Remove(data);
    }

    // Image Management
    public MarketComparableImage AddImage(
        Guid documentId,
        string? title = null,
        string? description = null)
    {
        var sequence = _images.Count > 0 ? _images.Max(i => i.DisplaySequence) + 1 : 1;
        var image = MarketComparableImage.Create(Id, sequence, documentId, title, description);
        _images.Add(image);
        return image;
    }

    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image != null)
            _images.Remove(image);
    }

    public void ReorderImages(IEnumerable<(Guid ImageId, int NewSequence)> reorderCommands)
    {
        foreach (var (imageId, newSequence) in reorderCommands)
        {
            var image = _images.FirstOrDefault(i => i.Id == imageId);
            image?.UpdateSequence(newSequence);
        }
    }
}