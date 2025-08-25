using Appraisal.AppraisalProperties.ValueObjects;

namespace Appraisal.AppraisalProperties.Models;

public class BuildingAppraisalDetail : Entity<long>
{
    public long ApprId { get; private set; }
    public BuildingInformation BuildingInformation { get; private set; } = default!;
    public BuildingTypeDetail BuildingTypeDetail { get; private set; } = default!;
    public DecorationDetail DecorationDetail { get; private set; } = default!;
    public Encroachment Encroachment { get; private set; } = default!;
    public BuildingConstructionInformation BuildingConstructionInformation { get; private set; } =
        default!;
    public string? BuildingMaterial { get; private set; }
    public string? BuildingStyle { get; private set; }
    public ResidentialStatus ResidentialStatus { get; private set; } = default!;
    public BuildingStructureDetail BuildingStructureDetail { get; private set; } = default!;
    public UtilizationDetail UtilizationDetail { get; private set; } = default!;
    public string? Remark { get; private set; }

    // BuildingAppraisalSurface
    private readonly List<BuildingAppraisalSurface> _buildingAppraisalSurfaces = [];
    public IReadOnlyList<BuildingAppraisalSurface> BuildingAppraisalSurfaces =>
        _buildingAppraisalSurfaces.AsReadOnly();

    // BuildingAppraisalDepreciationDetail
    private readonly List<BuildingAppraisalDepreciationDetail> _buildingAppraisalDepreciationDetails =
    [];
    public IReadOnlyList<BuildingAppraisalDepreciationDetail> BuildingAppraisalDepreciationDetails =>
        _buildingAppraisalDepreciationDetails.AsReadOnly();

    private BuildingAppraisalDetail() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private BuildingAppraisalDetail(
        long apprId,
        BuildingInformation buildingInformation,
        BuildingTypeDetail buildingTypeDetail,
        DecorationDetail decorationDetail,
        Encroachment encroachment,
        BuildingConstructionInformation buildingConstructionInformation,
        string? buildingMaterial,
        string? buildingStyle,
        ResidentialStatus residentialStatus,
        BuildingStructureDetail buildingStructureDetail,
        UtilizationDetail utilizationDetail,
        string? remark
    )
    {
        ApprId = apprId;
        BuildingInformation = buildingInformation;
        BuildingTypeDetail = buildingTypeDetail;
        DecorationDetail = decorationDetail;
        Encroachment = encroachment;
        BuildingConstructionInformation = buildingConstructionInformation;
        BuildingMaterial = buildingMaterial;
        BuildingStyle = buildingStyle;
        ResidentialStatus = residentialStatus;
        BuildingStructureDetail = buildingStructureDetail;
        UtilizationDetail = utilizationDetail;
        Remark = remark;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static BuildingAppraisalDetail Create(
        long apprId,
        BuildingInformation buildingInformation,
        BuildingTypeDetail buildingTypeDetail,
        DecorationDetail decorationDetail,
        Encroachment encroachment,
        BuildingConstructionInformation buildingConstructionInformation,
        string? buildingMaterial,
        string? buildingStyle,
        ResidentialStatus residentialStatus,
        BuildingStructureDetail buildingStructureDetail,
        UtilizationDetail utilizationDetail,
        string? remark
    )
    {
        return new BuildingAppraisalDetail(
            apprId,
            buildingInformation,
            buildingTypeDetail,
            decorationDetail,
            encroachment,
            buildingConstructionInformation,
            buildingMaterial,
            buildingStyle,
            residentialStatus,
            buildingStructureDetail,
            utilizationDetail,
            remark
        );
    }
}
