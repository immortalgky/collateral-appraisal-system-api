using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Extensions;
using Appraisal.AppraisalProperties.ValueObjects;

namespace Appraisal.AppraisalProperties.Models;

public class CondoAppraisalDetail : Entity<long>
{
    public long ApprId { get; private set; }
    public ObligationDetail ObligationDetail { get; private set; } = default!;
    public string? DocValidate { get; private set; }
    public CondominiumLocation CondominiumLocation { get; private set; } = default!;
    public CondoAttribute CondoAttribute { get; private set; } = default!;
    public Expropriation Expropriation { get; private set; } = default!;
    public CondominiumFacility CondominiumFacility { get; private set; } = default!;
    public CondoPrice CondoPrice { get; private set; } = default!;
    public ForestBoundary ForestBoundary { get; private set; } = default!;
    public string? Remark { get; private set; }

    // CondoAppraisalAreaDetail
    private readonly List<CondoAppraisalAreaDetail> _condoAppraisalAreaDetails = [];
    public IReadOnlyList<CondoAppraisalAreaDetail> CondoAppraisalAreaDetails =>
        _condoAppraisalAreaDetails.AsReadOnly();

    private CondoAppraisalDetail() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private CondoAppraisalDetail(
        long apprId,
        ObligationDetail obligationDetail,
        string? docValidate,
        CondominiumLocation condominiumLocation,
        CondoAttribute condoAttribute,
        Expropriation expropriation,
        CondominiumFacility condominiumFacility,
        CondoPrice condoPrice,
        ForestBoundary forestBoundary,
        string? remark
    )
    {
        ApprId = apprId;
        ObligationDetail = obligationDetail;
        DocValidate = docValidate;
        CondominiumLocation = condominiumLocation;
        CondoAttribute = condoAttribute;
        Expropriation = expropriation;
        CondominiumFacility = condominiumFacility;
        CondoPrice = condoPrice;
        ForestBoundary = forestBoundary;
        Remark = remark;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static CondoAppraisalDetail Create(
        long apprId,
        ObligationDetail obligationDetail,
        string? docValidate,
        CondominiumLocation condominiumLocation,
        CondoAttribute condoAttribute,
        Expropriation expropriation,
        CondominiumFacility condominiumFacility,
        CondoPrice condoPrice,
        ForestBoundary forestBoundary,
        string? remark
    )
    {
        return new CondoAppraisalDetail(
            apprId,
            obligationDetail,
            docValidate,
            condominiumLocation,
            condoAttribute,
            expropriation,
            condominiumFacility,
            condoPrice,
            forestBoundary,
            remark
        );
    }

    public void Update(CondoAppraisalDetail model)
    {
        ArgumentNullException.ThrowIfNull(model);

        ObligationDetail = model.ObligationDetail;
        DocValidate = model.DocValidate;
        CondominiumLocation = model.CondominiumLocation;
        CondoAttribute = model.CondoAttribute;
        Expropriation = model.Expropriation;
        CondominiumFacility = model.CondominiumFacility;
        CondoPrice = model.CondoPrice;
        ForestBoundary = model.ForestBoundary;
        Remark = model.Remark;

        _condoAppraisalAreaDetails.Clear();
        if (model.CondoAppraisalAreaDetails is not null)
        {
            _condoAppraisalAreaDetails.AddRange(model.CondoAppraisalAreaDetails);
        }
    }

    // Overload: Update using DTO (map DTO -> ValueObjects)
    public void Update(CondoAppraisalDetailDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ObligationDetail = dto.ObligationDetail.ToEntity();
        DocValidate = dto.DocValidate;
        CondominiumLocation = dto.CondominiumLocation.ToEntity();
        CondoAttribute = dto.CondoAttribute.ToEntity();
        Expropriation = dto.Expropriation.ToEntity();
        CondominiumFacility = dto.CondominiumFacility.ToEntity();
        CondoPrice = dto.CondoPrice.ToEntity();
        ForestBoundary = dto.ForestBoundary.ToEntity();
        Remark = dto.Remark;

        _condoAppraisalAreaDetails.Clear();
        if (dto.CondoAppraisalAreaDetails is not null)
        {
            foreach (var a in dto.CondoAppraisalAreaDetails)
            {
                _condoAppraisalAreaDetails.Add(CondoAppraisalAreaDetail.Create(a.AreaDesc, a.AreaSize));
            }
        }
    }
}
