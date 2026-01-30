namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Multiple title deeds per land property (adjacent plots grouped under one land).
/// </summary>
public class LandTitle : Entity<Guid>
{
    public Guid LandAppraisalDetailId { get; private set; }

    // Title Deed Info
    public string TitleDeedNumber { get; private set; } = default!;
    public string TitleDeedType { get; private set; } = default!;
    public string? BookNumber { get; private set; }
    public string? PageNumber { get; private set; }
    public string? LandParcelNumber { get; private set; }
    public string? SurveyNumber { get; private set; }
    public string? MapSheetNumber { get; private set; }
    public string? Rawang { get; private set; }
    public string? AerialMapName { get; private set; }
    public string? AerialMapNumber { get; private set; }

    // Area (Thai units)
    public LandArea? Area { get; private set; }

    // Boundary & Validation
    public bool? HasBoundaryMarker { get; private set; }
    public string? BoundaryMarkerRemark { get; private set; }
    public bool? IsDocumentValidated { get; private set; }
    public bool? IsMissingFromSurvey { get; private set; }

    // Pricing
    public decimal? GovernmentPricePerSqWa { get; private set; }
    public decimal? GovernmentPrice { get; private set; }

    // Remarks
    public string? Remark { get; private set; }

    private LandTitle()
    {
        // For EF Core
    }

    public static LandTitle Create(
        Guid landAppraisalDetailId,
        string titleDeedNumber,
        string titleDeedType)
    {
        return new LandTitle
        {
            LandAppraisalDetailId = landAppraisalDetailId,
            TitleDeedNumber = titleDeedNumber,
            TitleDeedType = titleDeedType
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        string? bookNumber,
        string? pageNumber,
        string? landParcelNumber,
        string? surveyNumber,
        string? mapSheetNumber,
        string? rawang,
        string? aerialMapName,
        string? aerialMapNumber,
        LandArea? area,
        bool? hasBoundaryMarker,
        string? boundaryMarkerRemark,
        bool? isDocumentValidated,
        bool? isMissingFromSurvey,
        decimal? governmentPricePerSqWa,
        decimal? governmentPrice,
        string? remark
    )
    {
        BookNumber = bookNumber;
        PageNumber = pageNumber;
        LandParcelNumber = landParcelNumber;
        SurveyNumber = surveyNumber;
        MapSheetNumber = mapSheetNumber;
        Rawang = rawang;
        AerialMapName = aerialMapName;
        AerialMapNumber = aerialMapNumber;
        Area = area;
        HasBoundaryMarker = hasBoundaryMarker;
        BoundaryMarkerRemark = boundaryMarkerRemark;
        IsDocumentValidated = isDocumentValidated;
        IsMissingFromSurvey = isMissingFromSurvey;
        GovernmentPricePerSqWa = governmentPricePerSqWa;
        GovernmentPrice = governmentPrice;
        Remark = remark;
    }
}