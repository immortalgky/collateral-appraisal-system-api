using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Extensions;
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
    private readonly List<BuildingAppraisalDepreciationDetail> _buildingAppraisalDepreciationDetails = [];
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
        string? remark,
        List<BuildingAppraisalSurface> buildingAppraisalSurfaces,
        List<BuildingAppraisalDepreciationDetail> buildingAppraisalDepreciationDetails
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

        _buildingAppraisalSurfaces.AddRange(buildingAppraisalSurfaces);
        _buildingAppraisalDepreciationDetails.AddRange(buildingAppraisalDepreciationDetails);

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
        string? remark,
        List<BuildingAppraisalSurface> buildingAppraisalSurfaces,
        List<BuildingAppraisalDepreciationDetail> buildingAppraisalDepreciationDetails
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
            remark,
            buildingAppraisalSurfaces,
            buildingAppraisalDepreciationDetails
        );
    }
    public void Update(BuildingAppraisalDetailDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        BuildingInformation = dto.BuildingInformation.ToEntity();
        BuildingTypeDetail = dto.BuildingTypeDetail.ToEntity();
        DecorationDetail = dto.DecorationDetail.ToEntity();
        Encroachment = dto.Encroachment.ToEntity();
        BuildingConstructionInformation = dto.BuildingConstructionInformation.ToEntity();
        BuildingMaterial = dto.BuildingMaterial;
        BuildingStyle = dto.BuildingStyle;
        ResidentialStatus = dto.ResidentialStatus.ToEntity();
        BuildingStructureDetail = dto.BuildingStructureDetail.ToEntity();
        UtilizationDetail = dto.UtilizationDetail.ToEntity();
        Remark = dto.Remark;
        UpdateBuildingAppraisalSurfaces(dto.BuildingAppraisalSurfaces);
        UpdateBuildingAppraisalDepreciationDetails(dto.BuildingAppraisalDepreciationDetails);
    }

    private void UpdateBuildingAppraisalSurfaces(IEnumerable<BuildingAppraisalSurfaceDto>? buildingAppraisalSurfaces)
    {
        _buildingAppraisalSurfaces.Clear();
        if (buildingAppraisalSurfaces is null) return;
        foreach (var s in buildingAppraisalSurfaces)
        {
            _buildingAppraisalSurfaces.Add(BuildingAppraisalSurface.Create(
                s.FromFloorNo,
                s.ToFloorNo,
                s.FloorType,
                s.FloorStructure,
                s.FloorStructureOther,
                s.FloorSurface,
                s.FloorSurfaceOther));
        }
    }

    private void UpdateBuildingAppraisalDepreciationDetails(IEnumerable<BuildingAppraisalDepreciationDetailDto>? buildingAppraisalDepreciationDetails)
    {
        _buildingAppraisalDepreciationDetails.Clear();
        if (buildingAppraisalDepreciationDetails is null) return;
        foreach (var d in buildingAppraisalDepreciationDetails)
        {
            _buildingAppraisalDepreciationDetails.Add(BuildingAppraisalDepreciationDetail.Create(
                d.AreaDesc,
                d.Area,
                d.PricePerSqM,
                d.PriceBeforeDegradation,
                d.Year,
                d.DegradationYearPct,
                d.TotalDegradationPct,
                d.PriceDegradation,
                d.TotalPrice,
                d.AppraisalMethod,
                d.BuildingAppraisalDepreciationPeriods?
                    .Select(p => p.ToEntity())
                    .ToList() ?? new List<BuildingAppraisalDepreciationPeriod>()
            ));
        }
    }
}
