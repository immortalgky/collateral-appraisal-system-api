namespace Request.Requests.ValueObjects;

public class RequestCustomer : ValueObject
{
    public string? Name { get; }
    public string? ContactNumber { get; }

#pragma warning disable CS8618
    public RequestCustomer()
    {
        // For EF Core
    }
#pragma warning restore CS8618

    private RequestCustomer(
        string name,
        string contactNumber
    )
    {
        Name = name;
        ContactNumber = contactNumber;
    }

    public static RequestCustomer Create(
        string? name,
        string? contactNumber
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactNumber);

        return new RequestCustomer(
            name,
            contactNumber
        );
    }
}