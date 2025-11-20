namespace Request.Requests.ValueObjects;

public class RequestDetail : ValueObject
{
    public bool HasAppraisalBook { get; } = false;
    public LoanDetail LoanDetail { get; } = default!;

    public long? PrevAppraisalNo { get; }

    // public Reference Reference { get; } = default!; // keep ony appraisal id to link
    public Address Address { get; } = default!;
    public Contact Contact { get; } = default!;
    public Appointment Appointment { get; } = default!;
    public Fee Fee { get; } = default!;
    // public Requestor Requestor { get; } = default!;

    private RequestDetail()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private RequestDetail(
        bool hasAppraisalBook,
        long? prevAppraisalNo,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee
        // Requestor requestor
    )
    {
        HasAppraisalBook = hasAppraisalBook;
        PrevAppraisalNo = prevAppraisalNo;
        LoanDetail = loanDetail;
        Address = address;
        Contact = contact;
        Appointment = appointment;
        Fee = fee;
        // Requestor = requestor;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static RequestDetail Create(
        bool hasAppraisalBook,
        long? prevAppraisalNo,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee
        // Requestor requestor
    )
    {
        ArgumentNullException.ThrowIfNull(loanDetail);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(fee);
        // ArgumentNullException.ThrowIfNull(requestor);

        return new RequestDetail(
            hasAppraisalBook,
            prevAppraisalNo,
            loanDetail,
            address,
            contact,
            appointment,
            fee);
        // requestor);
    }
}