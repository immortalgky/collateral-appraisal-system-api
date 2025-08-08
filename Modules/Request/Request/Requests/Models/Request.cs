using Request.RequestComments.Models;

namespace Request.Requests.Models;

public class Request : Aggregate<long>
{
    public AppraisalNumber? AppraisalNo { get; private set; }
    public RequestStatus Status { get; private set; } = default!;
    public RequestDetail Detail { get; private set; } = default!;

    // Customers
    private readonly List<RequestCustomer> _customers = [];
    public IReadOnlyList<RequestCustomer> Customers => _customers.AsReadOnly();

    // Properties
    private readonly List<RequestProperty> _properties = [];
    public IReadOnlyList<RequestProperty> Properties => _properties.AsReadOnly();

    private Request()
    {
        // For EF Core
    }

    private Request(RequestStatus status, RequestDetail detail)
    {
        Status = status;
        Detail = detail;

        AddDomainEvent(new RequestCreatedEvent(this));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Request Create(
        string purpose,
        bool hasAppraisalBook,
        string priority,
        string channel,
        int? occurConstInspec,
        Reference reference,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Fee fee,
        Requestor requestor
    )
    {
        var requestDetail = RequestDetail.Create(
            purpose,
            hasAppraisalBook,
            priority,
            channel,
            occurConstInspec,
            reference,
            loanDetail,
            address,
            contact,
            fee,
            requestor
        );

        return new Request(RequestStatus.New, requestDetail);
    }

    public void SetAppraisalNumber(AppraisalNumber appraisalNo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appraisalNo);

        AppraisalNo = appraisalNo;
    }

    private void UpdateStatus(RequestStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        Status = status;
    }

    public void SaveDraft()
    {
        UpdateStatus(RequestStatus.Draft);
    }

    public void Submit()
    {
        UpdateStatus(RequestStatus.New);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void UpdateDetail(
        string purpose,
        bool hasAppraisalBook,
        string priority,
        string channel,
        int? occurConstInspec,
        Reference reference,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Fee fee,
        Requestor requestor
    )
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.New,
                "Cannot update request details when the status is not Draft or New.")
            .ThrowIfInvalid();

        var newDetail = RequestDetail.Create(
            purpose,
            hasAppraisalBook,
            priority,
            channel,
            occurConstInspec,
            reference,
            loanDetail,
            address,
            contact,
            fee,
            requestor
        );

        if (!Detail.Equals(newDetail))
        {
            Detail = newDetail;
        }
    }

    public void UpdateCustomers(List<RequestCustomer> customers)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.New,
                "Cannot update request customers when the status is not Draft or New.")
            .ThrowIfInvalid();

        if (!_customers.SequenceEqual(customers))
        {
            _customers.Clear();
            _customers.AddRange(customers);
        }
    }

    public void UpdateProperties(List<RequestProperty> properties)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.New,
                "Cannot update request properties when the status is not Draft or New.")
            .ThrowIfInvalid();

        if (!_properties.SequenceEqual(properties))
        {
            _properties.Clear();
            _properties.AddRange(properties);
        }
    }

    public void AddCustomer(string name, string contactNumber)
    {
        RuleCheck.Valid()
            .AddErrorIf(_customers.Any(c => c.Name == name), "Customer with name '{name}' already exists.")
            .ThrowIfInvalid();

        var customer = RequestCustomer.Create(name, contactNumber);

        _customers.Add(customer);
    }

    public void RemoveCustomer(string name)
    {
        var initialCount = _customers.Count;
        var customers = _customers.Where(c => c.Name != name).ToList();

        RuleCheck.Valid()
            .AddErrorIf(initialCount == customers.Count, $"Customer with name '{name}' does not exist.")
            .ThrowIfInvalid();

        _customers.Clear();
        _customers.AddRange(customers);
    }

    public void AddProperty(string propertyType, string buildingType, decimal? sellingPrice)
    {
        RuleCheck.Valid()
            .AddErrorIf(_properties.Any(p => p.PropertyType == propertyType && p.BuildingType == buildingType),
                $"Property with type '{propertyType}' and building type '{buildingType}' already exists.")
            .ThrowIfInvalid();

        var property = RequestProperty.Of(propertyType, buildingType, sellingPrice);

        _properties.Add(property);
    }

    public void RemoveProperty(string propertyType, string buildingType)
    {
        var initialCount = _properties.Count;
        var properties = _properties.Where(c => c.PropertyType != propertyType && c.BuildingType != buildingType)
            .ToList();

        RuleCheck.Valid()
            .AddErrorIf(initialCount == properties.Count,
                $"Property with type '{propertyType}' and building type '{buildingType}' does not exist.")
            .ThrowIfInvalid();

        _properties.Clear();
        _properties.AddRange(properties);
    }
}