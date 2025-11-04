namespace Request.Requests.ValueObjects;

public class RequestDetail : ValueObject // [!] FK Request
{
    public bool HasAppraisalBook { get; }
    public bool IsPMA { get; }
    public string BankingSegment { get; }
    public Guid? PrevAppraisalId { get; }
    public LoanDetail LoanDetail { get; } = default!;
    public Address Address { get; } = default!;
    public Contact Contact { get; } = default!;
    public Fee Fee { get; } = default!;
    public Requestor Requestor { get; } = default!; // RequestedBy


    private RequestDetail()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private RequestDetail(
        bool hasAppraisalBook,
        bool isPMA,
        string bankingSegment,
        Guid? prevAppraisalId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Fee fee,
        Requestor requestor
    )
    {
        HasAppraisalBook = hasAppraisalBook;
        IsPMA = isPMA;
        BankingSegment = bankingSegment;
        PrevAppraisalId = prevAppraisalId;
        LoanDetail = loanDetail;
        Address = address;
        Contact = contact;
        Fee = fee;
        Requestor = requestor;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static RequestDetail Create(
        bool hasAppraisalBook,
        bool isPMA,
        string bankingSegment,
        Guid? prevAppraisalId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Fee fee,
        Requestor requestor
    )
    {
        ArgumentNullException.ThrowIfNull(bankingSegment);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(fee);
        ArgumentNullException.ThrowIfNull(requestor);

        return new RequestDetail(
            hasAppraisalBook,
            isPMA,
            bankingSegment,
            prevAppraisalId,
            loanDetail,
            address,
            contact,
            fee,
            requestor
        );
    }
}