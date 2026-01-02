namespace Request.Domain.Requests;

public class Contact : ValueObject
{
    public string? ContactPersonName { get; }
    public string? ContactPersonPhone { get; }
    public string? DealerCode { get; }

    private Contact(string? contactPersonName, string? contactPersonPhone, string? dealerCode)
    {
        ContactPersonName = contactPersonName;
        ContactPersonPhone = contactPersonPhone;
        DealerCode = dealerCode;
    }

    public static Contact Create(string? contactPersonName, string? contactPersonPhone, string? dealerCode = null)
    {
        return new Contact(contactPersonName, contactPersonPhone, dealerCode);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ContactPersonName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ContactPersonPhone);
    }
}