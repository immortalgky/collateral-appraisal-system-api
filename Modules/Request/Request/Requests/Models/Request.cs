using Request.RequestComments.Models;

namespace Request.Requests.Models;

public class Request : Aggregate<Guid> // Change `long` to `Guid`
{
    public RequestNumber? RequestNumber { get; private set; }
    public string? Purpose { get; private set; } = default!;

    // Channel, RequestDate, RequestedBy, RequestedByName
    public Source Source { get; private set; } = default!;
    public string Priority { get; private set; }
    public RequestStatus Status { get; private set; } = default!;
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // IsDeleted, DeleltedOn, DeletedBy
    public Deletion Deletion { get; private set; } = default!;
    public bool IsPMA { get; private set; }

    public DateTime? CreatedAt { get; private set; }
    public string? CreateBy { get; private set; }

    // Details
    public RequestDetail Detail { get; private set; } = default!;

    // Customers
    private readonly List<RequestCustomer> _customers = [];
    public IReadOnlyList<RequestCustomer> Customers => _customers.AsReadOnly();

    // Properties
    private readonly List<RequestProperty> _properties = [];
    public IReadOnlyList<RequestProperty> Properties => _properties.AsReadOnly();

    // Titles
    private readonly List<RequestTitle> _requestTitles = [];
    public IReadOnlyList<RequestTitle> RequestTitles => _requestTitles.AsReadOnly();


    private Request()
    {
        // For EF Core
    }

    private Request(
        RequestNumber? requestNumber, 
        string? purpose, 
        string priority, 
        RequestStatus requestStatus, 
        Deletion deletion, 
        bool isPMA, 
        RequestDetail detail
    )
    {
        RequestNumber = requestNumber;
        Purpose = purpose;
        Priority = priority;
        Status = requestStatus;
        Deletion = deletion;
        IsPMA = isPMA;
        Detail = detail;

        AddDomainEvent(new RequestCreatedEvent(this));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Request Create( 
        string? purpose, 
        string priority,
        RequestStatus requestStatus,
        bool isPMA, 
        bool hasOwnAppraisalBook,
        Guid? previousAppraisalId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee
    )
    {
        var requestDetail = RequestDetail.Create(
            hasOwnAppraisalBook,
            previousAppraisalId,
            loanDetail,
            address,
            contact,
            appointment,
            fee
        );

        return new Request(
            RequestNumber.Create("REQ-000001-2025"),
            purpose,
            priority,
            requestStatus,
            Deletion.NotDeleted(),
            isPMA,
            requestDetail
        );
    }

    // public void SetAppraisalNumber(AppraisalNumber appraisalNo)
    // {
    //     ArgumentException.ThrowIfNullOrWhiteSpace(appraisalNo);

    //     AppraisalNo = appraisalNo;
    // }

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
        UpdateStatus(RequestStatus.Submitted);
        // update SubmittedAt
        // update Source
    }

    public void UpdateSource(string requestedBy, string requestedByName, string? channel)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Submitted,
                "Cannot update request customers when the status is not Draft or New.")
            .ThrowIfInvalid();
        
        var newSource = Source.Create(requestedBy, requestedByName, channel);

        if (!Source.Equals(newSource))
            Source = newSource;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void UpdateDetail(
        bool hasAppraisalBook,
        Guid? previousAppraisaId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee
    )
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.Submitted,
                "Cannot update request details when the status is not Draft or New.")
            .ThrowIfInvalid();

        var newDetail = RequestDetail.Create(
            hasAppraisalBook,
            previousAppraisaId,
            loanDetail,
            address,
            contact,
            appointment,
            fee
        );

        if (!Detail.Equals(newDetail))
        {
            Detail = newDetail;
        }
    }

    public void UpdateCustomers(List<RequestCustomer> customers)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.Submitted,
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
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.Submitted,
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