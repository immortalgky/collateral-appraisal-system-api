namespace Request.Domain.Requests;

/// <summary>
/// Snapshot of the requestor's org-structure data captured at the time the request is created.
/// Stored as an owned entity on the Requests table so the data remains stable even if the
/// user's profile or org tables change later.
/// </summary>
public class Requestor : ValueObject
{
    public string? RequestorEmail { get; }
    public string? RequestorContactNo { get; }
    public string? RequestorAoCode { get; }
    public string? RequestorCostCenterCode { get; }
    public string? RequestorCostCenterDesc { get; }
    public string? RequestorDepartment { get; }

    private Requestor() { }

    private Requestor(
        string? email,
        string? contactNo,
        string? aoCode,
        string? costCenterCode,
        string? costCenterDesc,
        string? department)
    {
        RequestorEmail = email;
        RequestorContactNo = contactNo;
        RequestorAoCode = aoCode;
        RequestorCostCenterCode = costCenterCode;
        RequestorCostCenterDesc = costCenterDesc;
        RequestorDepartment = department;
    }

    public static Requestor Create(
        string? email,
        string? contactNo,
        string? aoCode,
        string? costCenterCode,
        string? costCenterDesc,
        string? department)
    {
        return new Requestor(email, contactNo, aoCode, costCenterCode, costCenterDesc, department);
    }
}
