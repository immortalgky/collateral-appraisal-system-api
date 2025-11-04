namespace Request.Requests.ValueObjects;

public class Contact : ValueObject
{
    public string ContactPersonName { get; }
    public string ContactPersonContactNo { get; }
    public string? ProjectCode { get; } // keep in string and use for find in parameter table

    private Contact(string contactPersonName, string contactPersonContactNo, string? projectCode)
    {
        ContactPersonName = contactPersonName;
        ContactPersonContactNo = contactPersonContactNo;
        ProjectCode = projectCode;
    }

    public static Contact Create(string contactPersonName, string contactPersonContactNo, string? projectCode = null)
    {
        return new Contact(contactPersonName, contactPersonContactNo, projectCode);
    }
}