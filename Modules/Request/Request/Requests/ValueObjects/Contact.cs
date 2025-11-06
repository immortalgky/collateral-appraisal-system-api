namespace Request.Requests.ValueObjects;

public class Contact : ValueObject
{
    public string? ContactPersonName { get; }
    public string? ContactPersonPhone { get; }
    public string? ProjectCode { get; } 

    private Contact(string? contactPersonName, string? contactPersonPhone, string? projectCode)
    {
        ContactPersonName = contactPersonName;
        ContactPersonPhone = contactPersonPhone;
        ProjectCode = projectCode;
    }

    public static Contact Create(string? contactPersonName, string? contactPersonPhone, string? projectCode = null)
    {
        return new Contact(contactPersonName, contactPersonPhone, projectCode);
    }
}