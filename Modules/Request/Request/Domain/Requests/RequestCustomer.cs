namespace Request.Domain.Requests;

public class RequestCustomer : ValueObject
{
    public string? Name { get; }
    public string? ContactNumber { get; }

    private RequestCustomer(string? name, string? contactNumber)
    {
        Name = name;
        ContactNumber = contactNumber;
    }

    public static RequestCustomer Create(string? name, string? contactNumber)
    {
        return new RequestCustomer(name, contactNumber);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ContactNumber);
    }
}