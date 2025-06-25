namespace Request.Requests.Models;

public class Request : Aggregate<long>
{
    private readonly List<RequestCustomer> _customers = [];
    private readonly List<RequestProperty> _property = [];

    private Request()
    {
    }

    private Request(string appraisalNo, string status, RequestDetail detail)
    {
        AppraisalNo = appraisalNo;
        Status = status;
        Detail = detail;

        AddDomainEvent(new RequestCreatedEvent(this));
    }

    public string AppraisalNo { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public RequestDetail Detail { get; private set; } = default!;
    public IReadOnlyList<RequestCustomer> Customers => _customers.AsReadOnly();
    public IReadOnlyList<RequestProperty> Property => _property.AsReadOnly();

    // Method
    public static Request From(RequestDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var requestDetail = RequestDetail.Of(
            detail.Purpose,
            detail.HasAppraisalBook,
            detail.Priority,
            detail.Channel,
            detail.LoanApplicationNo,
            detail.LimitAmt,
            detail.OccurConstInspec,
            detail.TotalSellingPrice,
            new Reference(detail.Reference.PrevAppraisalNo,
                detail.Reference.PrevAppraisalValue, detail.Reference.PrevAppraisalDate),
            Address.Create(detail.Address.HouseNo, detail.Address.RoomNo, detail.Address.FloorNo,
                detail.Address.LocationIdentifier, detail.Address.Moo, detail.Address.Soi, detail.Address.Road,
                detail.Address.SubDistrict,
                detail.Address.District, detail.Address.Province, detail.Address.Postcode),
            new Contact(detail.Contact.ContactPersonName, detail.Contact.ContactPersonContactNo,
                detail.Contact.ProjectCode),
            new Fee(detail.Fee.FeeType, detail.Fee.FeeRemark),
            Requestor.Create(detail.Requestor.RequestorEmpId, detail.Requestor.RequestorName,
                detail.Requestor.RequestorEmail, detail.Requestor.RequestorContactNo, detail.Requestor.RequestorAo,
                detail.Requestor.RequestorBranch, detail.Requestor.RequestorBusinessUnit,
                detail.Requestor.RequestorDepartment,
                detail.Requestor.RequestorSection, detail.Requestor.RequestorCostCenter)
        );

        return new Request("67A", "N", requestDetail);
    }

    public void UpdateStatus(string status)
    {
        ArgumentException.ThrowIfNullOrEmpty(status);

        Status = status;
    }

    public void UpdateDetail(RequestDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var requestDetail = RequestDetail.Of(
            detail.Purpose,
            detail.HasAppraisalBook,
            detail.Priority,
            detail.Channel,
            detail.LoanApplicationNo,
            detail.LimitAmt,
            detail.OccurConstInspec,
            detail.TotalSellingPrice,
            new Reference(detail.Reference.PrevAppraisalNo,
                detail.Reference.PrevAppraisalValue, detail.Reference.PrevAppraisalDate),
            Address.Create(detail.Address.HouseNo, detail.Address.RoomNo, detail.Address.FloorNo,
                detail.Address.LocationIdentifier, detail.Address.Moo, detail.Address.Soi, detail.Address.Road,
                detail.Address.SubDistrict,
                detail.Address.District, detail.Address.Province, detail.Address.Postcode),
            new Contact(detail.Contact.ContactPersonName, detail.Contact.ContactPersonContactNo,
                detail.Contact.ProjectCode),
            new Fee(detail.Fee.FeeType, detail.Fee.FeeRemark),
            Requestor.Create(detail.Requestor.RequestorEmpId, detail.Requestor.RequestorName,
                detail.Requestor.RequestorEmail, detail.Requestor.RequestorContactNo, detail.Requestor.RequestorAo,
                detail.Requestor.RequestorBranch, detail.Requestor.RequestorBusinessUnit,
                detail.Requestor.RequestorDepartment,
                detail.Requestor.RequestorSection, detail.Requestor.RequestorCostCenter)
        );

        Detail = requestDetail;
    }

    public void AddCustomer(RequestCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        _customers.Add(customer);
    }

    public void AddProperty(RequestProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        _property.Add(property);
    }

    public void ClearProperty()
    {
        _property.Clear();
    }
}