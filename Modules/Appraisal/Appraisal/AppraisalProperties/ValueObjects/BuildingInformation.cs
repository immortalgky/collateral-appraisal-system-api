namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingInformation : ValueObject
{
    public string NoHouseNumber { get; } = default!;
    public decimal? LandArea { get; }
    public string? BuildingCondition { get; }
    public string? BuildingStatus { get; }
    public DateTime? LicenseExpirationDate { get; }
    public string? IsAppraise { get; }
    public ObligationDetail ObligationDetail { get; } = default!;

    private BuildingInformation() { }

    private BuildingInformation(
        string noHouseNumber,
        decimal? landArea,
        string buildingCondition,
        string buildingStatus,
        DateTime? licenseExpirationDate,
        string isAppraise,
        ObligationDetail obligationDetail
    )
    {
        NoHouseNumber = noHouseNumber;
        LandArea = landArea;
        BuildingCondition = buildingCondition;
        BuildingStatus = buildingStatus;
        LicenseExpirationDate = licenseExpirationDate;
        IsAppraise = isAppraise;
        ObligationDetail = obligationDetail;
    }

    public static BuildingInformation Create(
        string noHouseNumber,
        decimal? landArea,
        string buildingCondition,
        string buildingStatus,
        DateTime? licenseExpirationDate,
        string isAppraise,
        ObligationDetail obligationDetail
    )
    {
        return new BuildingInformation(
            noHouseNumber,
            landArea,
            buildingCondition,
            buildingStatus,
            licenseExpirationDate,
            isAppraise,
            obligationDetail
        );
    }
}
