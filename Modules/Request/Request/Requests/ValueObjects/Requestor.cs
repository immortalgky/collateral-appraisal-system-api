namespace Request.Requests.ValueObjects;

public class Requestor : ValueObject // In user table, when requestor expire we will only change status of user to expire or sth. so, In request we will keep only RequesterId.
{
    public string RequestorEmpId { get; } = default!;
    public string RequestorName { get; } = default!;
    public string RequestorEmail { get; } = default!;
    public string RequestorContactNo { get; } = default!;
    public string RequestorAo { get; } = default!;
    public string RequestorBranch { get; } = default!;
    public string RequestorBusinessUnit { get; } = default!;
    public string RequestorDepartment { get; } = default!;
    public string RequestorSection { get; } = default!;
    public string RequestorCostCenter { get; } = default!;
    
    private Requestor(
        string requestorEmpId,
        string requestorName,
        string requestorEmail,
        string requestorContactNo,
        string requestorAo,
        string requestorBranch,
        string requestorBusinessUnit,
        string requestorDepartment,
        string requestorSection,
        string requestorCostCenter
    )
    {
        RequestorEmpId = requestorEmpId;
        RequestorName = requestorName;
        RequestorEmail = requestorEmail;
        RequestorContactNo = requestorContactNo;
        RequestorAo = requestorAo;
        RequestorBranch = requestorBranch;
        RequestorBusinessUnit = requestorBusinessUnit;
        RequestorDepartment = requestorDepartment;
        RequestorSection = requestorSection;
        RequestorCostCenter = requestorCostCenter;
    }


    public static Requestor Create(
        string requestorEmpId,
        string requestorName,
        string requestorEmail,
        string requestorContactNo,
        string requestorAo,
        string requestorBranch,
        string requestorBusinessUnit,
        string requestorDepartment,
        string requestorSection,
        string requestorCostCenter
    )
    {
        return new Requestor(
            requestorEmpId,
            requestorName,
            requestorEmail,
            requestorContactNo,
            requestorAo,
            requestorBranch,
            requestorBusinessUnit,
            requestorDepartment,
            requestorSection,
            requestorCostCenter
        );
    }
}