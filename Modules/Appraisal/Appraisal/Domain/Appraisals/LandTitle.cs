namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Multiple title deeds per land property (adjacent plots grouped under one land).
/// </summary>
public class LandTitle : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }

    // Sequence
    public int SequenceNumber { get; private set; }

    // Title Deed Info
    public string TitleDeedNumber { get; private set; } = null!; // โฉนดเลขที่
    public string? BookNumber { get; private set; } // เล่ม
    public string? PageNumber { get; private set; } // หน้า
    public string? LandNumber { get; private set; } // เลขที่ดิน
    public string? SurveyNumber { get; private set; } // หน้าสำรวจ
    public string? SheetNumber { get; private set; } // ระวาง

    // Document Type
    public string DocumentType { get; private set; } = null!; // Chanote, NorSor3Gor, NorSor3
    public string? Rawang { get; private set; } // ระวาง
    public string? AerialPhotoNumber { get; private set; }
    public string? AerialPhotoName { get; private set; }

    // Area (Thai units)
    public int? AreaRai { get; private set; } // ไร่
    public int? AreaNgan { get; private set; } // งาน
    public decimal? AreaSquareWa { get; private set; } // ตารางวา
    public decimal? TotalAreaInSquareWa { get; private set; } // Total in sq.wa (calculated)

    // Boundary & Validation
    public string? BoundaryMarker { get; private set; } // หลักเขต
    public string? BoundaryMarkerOther { get; private set; }
    public string? DocumentValidation { get; private set; } // Valid, Invalid, Pending
    public bool IsMissedOutSurvey { get; private set; } // ตกสำรวจ

    // Pricing
    public decimal? PricePerSquareWa { get; private set; } // Market price per sq.wa
    public decimal? GovernmentPrice { get; private set; } // ราคาประเมินกรมที่ดิน

    // Remarks
    public string? Remarks { get; private set; }

    private LandTitle()
    {
    }

    public static LandTitle Create(
        Guid appraisalPropertyId,
        int sequenceNumber,
        string titleDeedNumber,
        string documentType)
    {
        return new LandTitle
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            SequenceNumber = sequenceNumber,
            TitleDeedNumber = titleDeedNumber,
            DocumentType = documentType,
            IsMissedOutSurvey = false
        };
    }

    public void SetDocumentInfo(
        string? bookNumber,
        string? pageNumber,
        string? landNumber,
        string? surveyNumber,
        string? sheetNumber)
    {
        BookNumber = bookNumber;
        PageNumber = pageNumber;
        LandNumber = landNumber;
        SurveyNumber = surveyNumber;
        SheetNumber = sheetNumber;
    }

    public void SetArea(int? rai, int? ngan, decimal? squareWa)
    {
        AreaRai = rai;
        AreaNgan = ngan;
        AreaSquareWa = squareWa;

        // Calculate total in square wa: 1 rai = 400 sq.wa, 1 ngan = 100 sq.wa
        TotalAreaInSquareWa = (rai ?? 0) * 400 + (ngan ?? 0) * 100 + (squareWa ?? 0);
    }

    public void SetBoundary(string? marker, string? markerOther)
    {
        BoundaryMarker = marker;
        BoundaryMarkerOther = markerOther;
    }

    public void SetValidation(string? validation, bool isMissedOutSurvey = false)
    {
        DocumentValidation = validation;
        IsMissedOutSurvey = isMissedOutSurvey;
    }

    public void SetPricing(decimal? pricePerSquareWa, decimal? governmentPrice)
    {
        PricePerSquareWa = pricePerSquareWa;
        GovernmentPrice = governmentPrice;
    }

    public void SetRemarks(string? remarks)
    {
        Remarks = remarks;
    }
}