using Request.RequestComments.Models;

namespace Request.Requests.Models;

public class Request : Aggregate<Guid>
{
    public AppraisalNumber AppraisalNo { get; private set; }
    public string Purpose { get; private set; }
    public SourceSystem SourceSystem { get; private set; }
    public string Priority { get; private set; } = default!;
    public RequestStatus Status { get; private set; } = default!;
    public SoftDelete SoftDelete { get; private set; }
    public bool IsPMA { get; private set; }
    public RequestDetail Detail { get; private set; } = default!;


    // Customers
    private readonly List<RequestCustomer> _customers = [];
    public IReadOnlyList<RequestCustomer> Customers => _customers.AsReadOnly();

    // Properties
    private readonly List<RequestProperty> _properties = [];
    public IReadOnlyList<RequestProperty> Properties => _properties.AsReadOnly();

    // Documents
    private readonly List<RequestDocument> _documents = [];
    public IReadOnlyList<RequestDocument> Documents => _documents.AsReadOnly();

    private Request()
    {
        // For EF Core
    }

    private Request(RequestStatus status, RequestDetail detail, bool isPMA, string purpose,
        string priority, SourceSystem sourceSystem, SoftDelete softDelete)
    {
        Id = Guid.NewGuid();
        Status = status;
        Detail = detail;
        IsPMA = isPMA;
        Purpose = purpose;
        Priority = priority;
        SoftDelete = softDelete;
        SourceSystem = sourceSystem;

        AddDomainEvent(new RequestCreatedEvent(this));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Request Create(
        RequestDetail requestDetail,
        bool isPMA,
        string purpose,
        string priority,
        SourceSystem sourceSystem
    )
    {
        var detail = RequestDetail.Create(
            requestDetail.HasAppraisalBook,
            requestDetail.PrevAppraisalNo,
            requestDetail.LoanDetail,
            requestDetail.Address,
            requestDetail.Contact,
            requestDetail.Appointment,
            requestDetail.Fee
        );
        var softDelete = SoftDelete.Create(false, null, null);

        return new Request(RequestStatus.New, detail, isPMA, purpose, priority, sourceSystem, softDelete);
    }

    public void UpdateRequest(
        string purpose,
        SourceSystem sourceSystem,
        string priority,
        bool isPMA,
        RequestDetail detail,
        List<RequestCustomer> customers,
        List<RequestProperty> properties)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.New,
                "Cannot update request when the status is not Draft or New.")
            .ThrowIfInvalid();
        Purpose = purpose;
        SourceSystem = sourceSystem;
        Priority = priority;
        IsPMA = isPMA;
        var newDetail = RequestDetail.Create(
            detail.HasAppraisalBook,
            detail.PrevAppraisalNo,
            detail.LoanDetail,
            detail.Address,
            detail.Contact,
            detail.Appointment,
            detail.Fee
        );

        if (!Detail.Equals(newDetail))
        {
            Detail = newDetail;
        }

        if (!_customers.SequenceEqual(customers))
        {
            _customers.Clear();
            _customers.AddRange(customers);
        }

        if (!_properties.SequenceEqual(properties))
        {
            _properties.Clear();
            _properties.AddRange(properties);
        }
    }


    public static Request CreateDraft(
        RequestDetail requestDetail,
        bool isPMA,
        string purpose,
        string priority,
        SourceSystem sourceSystem
    )
    {
        var detail = RequestDetail.Create(
            requestDetail.HasAppraisalBook,
            requestDetail.PrevAppraisalNo,
            requestDetail.LoanDetail,
            requestDetail.Address,
            requestDetail.Contact,
            requestDetail.Appointment,
            requestDetail.Fee
        );
        var softDelete = SoftDelete.Create(false, null, null);

        return new Request(RequestStatus.Draft, detail, isPMA, purpose, priority, sourceSystem, softDelete);
    }

    public void SetAppraisalNumber(AppraisalNumber appraisalNo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appraisalNo);

        AppraisalNo = appraisalNo;
    }

    private void UpdateStatus(RequestStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status == RequestStatus.Submitted)
        {
            var newStatus = RequestStatus.UpdateDate(status, DateTime.Now, null);
            Status = newStatus;
        }
        else if (status == RequestStatus.Completed)
        {
            var existingSubmitted = Status.SubmittedAt;
            var newStatus = RequestStatus.UpdateDate(status, existingSubmitted, DateTime.Now);
            Status = newStatus;
        }
        else
        {
            Status = status;
        }
    }

    public void UpdateIsDelete()
    {
        var isDeleted = SoftDelete.Create(
            true, DateTime.Now, "01"
        );

        SoftDelete = isDeleted;
    }

    public void Submit()
    {
        UpdateStatus(RequestStatus.Submitted);
    }

    public void Completed()
    {
        UpdateStatus(RequestStatus.Completed);
    }


    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void AddCustomer(string name, string contactNumber)
    {
        RuleCheck.Valid()
            .AddErrorIf(_customers.Any(c => c.Name == name), $"Customer with name '{name}' already exists.")
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