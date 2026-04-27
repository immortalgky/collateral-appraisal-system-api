namespace Appraisal.Domain.Projects;

/// <summary>
/// Individual title deed record belonging to a ProjectLand.
/// Moved from VillageProjectLandTitle; FK changed from VillageProjectLandId to ProjectLandId.
/// Only relevant when ProjectType = LandAndBuilding.
/// </summary>
public class ProjectLandTitle : Entity<Guid>
{
    public Guid ProjectLandId { get; private set; }

    // Title Deed Info
    public string TitleNumber { get; private set; } = default!;
    public string TitleType { get; private set; } = default!;
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
    public string? BoundaryMarkerType { get; private set; }
    public string? BoundaryMarkerRemark { get; private set; }
    public string? DocumentValidationResultType { get; private set; }
    public bool? IsMissingFromSurvey { get; private set; }

    // Pricing
    public decimal? GovernmentPricePerSqWa { get; private set; }
    public decimal? GovernmentPrice { get; private set; }

    // Remarks
    public string? Remark { get; private set; }

    private ProjectLandTitle()
    {
    }

    public static ProjectLandTitle Create(
        Guid projectLandId,
        string titleNumber,
        string titleType)
    {
        return new ProjectLandTitle
        {
            Id = Guid.CreateVersion7(),
            ProjectLandId = projectLandId,
            TitleNumber = titleNumber,
            TitleType = titleType
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
        string? boundaryMarkerType,
        string? boundaryMarkerRemark,
        string? documentValidationResultType,
        bool? isMissingFromSurvey,
        decimal? governmentPricePerSqWa,
        decimal? governmentPrice,
        string? remark)
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
        BoundaryMarkerType = boundaryMarkerType;
        BoundaryMarkerRemark = boundaryMarkerRemark;
        DocumentValidationResultType = documentValidationResultType;
        IsMissingFromSurvey = isMissingFromSurvey;
        GovernmentPricePerSqWa = governmentPricePerSqWa;
        GovernmentPrice = governmentPrice;
        Remark = remark;
    }
}
