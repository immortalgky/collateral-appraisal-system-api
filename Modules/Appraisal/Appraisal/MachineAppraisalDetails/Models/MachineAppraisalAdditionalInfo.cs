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
}