using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.AppraisalProperties.ValueObjects;
namespace Appraisal.MachineAppraisalDetails.Models;

public class MachineAppraisalAdditionalInfo : Entity<long>
{
    public long ApprId { get; private set; } = default!;
    public PurposeAndLocationMachine PurposeAndLocationMachine { get; private set; } = default!;
    public MachineDetail MachineDetail { get; private set; } = default!;

    private MachineAppraisalAdditionalInfo() { }


    private MachineAppraisalAdditionalInfo(
        long apprId,
        PurposeAndLocationMachine purposeAndLocationMachine,
        MachineDetail machineDetail
    )
    {
        ApprId = apprId;
        PurposeAndLocationMachine = purposeAndLocationMachine;
        MachineDetail = machineDetail;
    }

    public static MachineAppraisalAdditionalInfo Create(
        long apprId,
        PurposeAndLocationMachine purposeAndLocationMachine,
        MachineDetail machineDetail
    )
    {
        return new MachineAppraisalAdditionalInfo(
            apprId,
            purposeAndLocationMachine,
            machineDetail
        );
    }

    public void Update(MachineAppraisalAdditionalInfo model)
    {
        ArgumentNullException.ThrowIfNull(model);
        PurposeAndLocationMachine = model.PurposeAndLocationMachine;
        MachineDetail = model.MachineDetail;
    }

    // Overload: Update using DTO (map DTO -> ValueObjects)
    public void Update(MachineAppraisalAdditionalInfoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        PurposeAndLocationMachine = PurposeAndLocationMachine.Create(
            dto.Assignment ?? string.Empty,
            dto.ApprCollatPurpose ?? string.Empty,
            dto.ApprDate ?? string.Empty,
            dto.ApprCollatType ?? string.Empty
        );

        var generalMachinery = GeneralMachinery.Crate(
            dto.Industrial,
            dto.SurveyNo,
            dto.ApprNo
        );
        var atSurvey = AtSurveyDate.Create(
            dto.Installed ?? 0,
            dto.ApprScrap ?? string.Empty,
            dto.NoOfAppraise ?? 0,
            dto.NotInstalled ?? 0,
            dto.Maintenance ?? string.Empty,
            dto.Exterior ?? string.Empty,
            dto.Performance ?? string.Empty,
            dto.MarketDemand ?? false,
            dto.MarketDemandRemark ?? string.Empty
        );
        var rights = RightsAndConditionsOfLegalRestrictions.Crate(
            dto.Proprietor ?? string.Empty,
            dto.Owner ?? string.Empty,
            dto.MachineLocation ?? string.Empty,
            dto.Obligation ?? string.Empty,
            dto.Other ?? string.Empty
        );
        MachineDetail = MachineDetail.Create(generalMachinery, atSurvey, rights);
    }
}