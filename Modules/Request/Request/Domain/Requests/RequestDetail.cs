namespace Request.Domain.Requests;

public class RequestDetail : ValueObject
{
    public bool HasAppraisalBook { get; }
    public LoanDetail? LoanDetail { get; }
    public Guid? PrevAppraisalId { get; }
    public string? PrevAppraisalNumber { get; }
    public decimal? PrevAppraisalValue { get; }
    public DateTime? PrevAppraisalDate { get; }
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
        PrevAppraisalNumber = data.PrevAppraisalNumber;
        PrevAppraisalValue = data.PrevAppraisalValue;
        PrevAppraisalDate = data.PrevAppraisalDate;
        Address = data.Address;
        Contact = data.Contact;
        Appointment = data.Appointment;
        Fee = data.Fee;
    }

    public static RequestDetail Create(RequestDetailData data)
    {
        return new RequestDetail(data);
    }

    /// <summary>
    /// This detail with a prior book that exists only in AS400 (see Request.SetLegacyPriorBook). The
    /// owned children are copied, not shared: they are tracked as owned by THIS detail, and EF refuses
    /// to move a tracked owned instance to a new owner.
    /// </summary>
    internal RequestDetail WithLegacyPriorBook(string number, decimal? value, DateTime? date) =>
        Copy(prevAppraisalId: null, number, value, date);

    /// <summary>This detail with another prior value and date, everything else unchanged (owned children copied).</summary>
    internal RequestDetail WithPriorValue(decimal? value, DateTime? date) =>
        Copy(PrevAppraisalId, PrevAppraisalNumber, value, date);

    private RequestDetail Copy(Guid? prevAppraisalId, string? number, decimal? value, DateTime? date) =>
        new(new RequestDetailData(
            HasAppraisalBook,
            LoanDetail is null ? null : LoanDetail.Create(new LoanDetailData(
                LoanDetail.BankingSegment, LoanDetail.LoanApplicationNumber, LoanDetail.FacilityLimit,
                LoanDetail.AdditionalFacilityLimit, LoanDetail.PreviousFacilityLimit, LoanDetail.TotalSellingPrice)),
            prevAppraisalId,
            Address is null ? null : Address.Create(new AddressData(
                Address.HouseNumber, Address.ProjectName, Address.Moo, Address.Soi, Address.Road,
                Address.SubDistrict, Address.District, Address.Province, Address.Postcode)),
            Contact is null ? null : Contact.Create(Contact.ContactPersonName, Contact.ContactPersonPhone, Contact.DealerCode),
            Appointment is null ? null : Appointment.Create(Appointment.AppointmentDateTime, Appointment.AppointmentLocation),
            Fee is null ? null : Fee.Create(Fee.FeePaymentType, Fee.FeeNotes, Fee.AbsorbedAmount),
            number, value, date));

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
    Fee? Fee,
    string? PrevAppraisalNumber = null,
    decimal? PrevAppraisalValue = null,
    DateTime? PrevAppraisalDate = null
);