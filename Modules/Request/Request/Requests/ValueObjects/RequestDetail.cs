namespace Request.Requests.ValueObjects;

public class RequestDetail : ValueObject // [!] FK Request
{
    public string Purpose { get; } = default!; // move to Request
    public bool HasAppraisalBook { get; }
    public string Priority { get; } = default!; // move to Request
    public string Channel { get; } = default!; // move to Request
    public int? OccurConstInspec { get; } // move to ConstructionInspectionMaster
    public LoanDetail LoanDetail { get; } = default!;
    public Reference Reference { get; } = default!;
    public Address Address { get; } = default!;
    public Contact Contact { get; } = default!;
    public Fee Fee { get; } = default!;
    public Requestor Requestor { get; } = default!; // [????] rename to RequestedBy; FK to User // on hold

    // ADD: IsPMA
    // ADD: BankingSegment

    private RequestDetail()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private RequestDetail(
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
        Purpose = purpose;
        HasAppraisalBook = hasAppraisalBook;
        Priority = priority;
        Channel = channel;
        OccurConstInspec = occurConstInspec;
        Reference = reference;
        LoanDetail = loanDetail;
        Address = address;
        Contact = contact;
        Fee = fee;
        Requestor = requestor;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static RequestDetail Create(
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
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentNullException.ThrowIfNull(priority);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(fee);
        ArgumentNullException.ThrowIfNull(requestor);

        return new RequestDetail(
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
            requestor);
    }
}