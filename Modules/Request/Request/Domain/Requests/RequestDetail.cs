namespace Request.Domain.Requests;

public class RequestDetail : ValueObject
{
    public bool HasAppraisalBook { get; }
    public LoanDetail? LoanDetail { get; }
    public Guid? PrevAppraisalId { get; }
    public Address? Address { get; }
    public Contact? Contact { get; }
    public Appointment? Appointment { get; }
    public Fee? Fee { get; }

    private RequestDetail()
    {
        // For EF Core
    }

    private RequestDetail(RequestDetailData data)
    {
        HasAppraisalBook = data.HasAppraisalBook;
        LoanDetail = data.LoanDetail;
        PrevAppraisalId = data.PrevAppraisalId;
        Address = data.Address;
        Contact = data.Contact;
        Appointment = data.Appointment;
        Fee = data.Fee;
    }

    public static RequestDetail Create(RequestDetailData data)
    {
        return new RequestDetail(data);
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(LoanDetail);
        ArgumentNullException.ThrowIfNull(Address);
        ArgumentNullException.ThrowIfNull(Contact);
        ArgumentNullException.ThrowIfNull(Appointment);
        ArgumentNullException.ThrowIfNull(Fee);

        LoanDetail.Validate();
        Address.Validate();
        Contact.Validate();
        Appointment.Validate();
        Fee.Validate();
    }
}

public record RequestDetailData(
    bool HasAppraisalBook,
    LoanDetail? LoanDetail,
    Guid? PrevAppraisalId,
    Address? Address,
    Contact? Contact,
    Appointment? Appointment,
    Fee? Fee
);