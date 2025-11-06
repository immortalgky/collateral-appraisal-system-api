namespace Request.Requests.ValueObjects;

public class RequestDetail : ValueObject // [!] FK Request
{
    public bool HasOwnAppraisalBook { get; }
    public Guid? PreviousAppraisalId { get; }
    
    // Loan Information
    public LoanDetail LoanDetail { get; } = default!;

    // Location
    public Address Address { get; } = default!;
    public Contact Contact { get; } = default!;

    // Appointment
    public Appointment Appointment { get; }

    // Fee
    public Fee Fee { get; } = default!;


    private RequestDetail()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private RequestDetail(
        bool hasOwnAppraisalBook,
        Guid? previousAppraisalId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee
    )
    {
        HasOwnAppraisalBook = hasOwnAppraisalBook;
        PreviousAppraisalId = previousAppraisalId;
        LoanDetail = loanDetail;
        Address = address;
        Contact = contact;
        Appointment = appointment;
        Fee = fee;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static RequestDetail Create(
        bool hasOwnAppraisalBook,
        Guid? previousAppraisalId,
        LoanDetail loanDetail,
        Address address,
        Contact contact,
        Appointment appointment,
        Fee fee
    )
    {
        ArgumentNullException.ThrowIfNull(loanDetail);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(fee);

        return new RequestDetail(
            hasOwnAppraisalBook,
            previousAppraisalId,
            loanDetail,
            address,
            contact,
            appointment,
            fee
        );
    }
}