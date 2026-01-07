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

    // Soft Delete
    public SoftDelete SoftDelete { get; private set; } = SoftDelete.NotDeleted();

    private MarketComparable()
    {
    }

    public static MarketComparable Create(
        string comparableNumber,
        string propertyType,
        string province,
        string dataSource,
        DateTime surveyDate)
    {
        return new MarketComparable
        {
            Id = Guid.NewGuid(),
            ComparableNumber = comparableNumber,
            PropertyType = propertyType,
            Province = province,
            DataSource = dataSource,
            SurveyDate = surveyDate,
            Status = "Active"
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
}