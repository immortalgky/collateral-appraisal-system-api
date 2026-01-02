namespace Appraisal.MachineAppraisalDetails.ValueObjects;

public class PurposeAndLocationMachine : ValueObject
{
    public string? Assignment { get; }
    public string? ApprCollatPurpose { get; }
    public string? ApprDate { get; }
    public string? ApprCollatType { get; }

    private PurposeAndLocationMachine() { }
    private PurposeAndLocationMachine(
        string assignment,
        string apprCollatPurpose,
        string apprDate,
        string apprCollatType
    )
    {
        Assignment = assignment;
        ApprCollatPurpose = apprCollatPurpose;
        ApprDate = apprDate;
        ApprCollatType = apprCollatType;
    }

    public static PurposeAndLocationMachine Create(
        string assignment,
        string apprCollatPurpose,
        string apprDate,
        string apprCollatType
    )
    {
        return new PurposeAndLocationMachine(assignment, apprCollatPurpose, apprDate, apprCollatType);
    }
}