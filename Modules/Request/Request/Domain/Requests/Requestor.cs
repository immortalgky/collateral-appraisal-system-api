namespace Request.Domain.Requests;

public class Requestor : ValueObject
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