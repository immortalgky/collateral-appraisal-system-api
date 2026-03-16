using Shared.DDD;

namespace Auth.Domain.Companies;

public class Company : Entity<Guid>
{
    public string Name { get; private set; } = default!;
    public string? TaxId { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Street { get; private set; }
    public string? City { get; private set; }
    public string? Province { get; private set; }
    public string? PostalCode { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedOn { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Company() { }

    public static Company Create(
        string name,
        string? taxId = null,
        string? phone = null,
        string? email = null,
        string? street = null,
        string? city = null,
        string? province = null,
        string? postalCode = null)
    {
        return new Company
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            TaxId = taxId,
            Phone = phone,
            Email = email,
            Street = street,
            City = city,
            Province = province,
            PostalCode = postalCode,
            IsActive = true,
            IsDeleted = false
        };
    }

    public void Update(
        string name,
        string? taxId,
        string? phone,
        string? email,
        string? street,
        string? city,
        string? province,
        string? postalCode,
        bool isActive)
    {
        Name = name;
        TaxId = taxId;
        Phone = phone;
        Email = email;
        Street = street;
        City = city;
        Province = province;
        PostalCode = postalCode;
        IsActive = isActive;
    }

    public void Delete(Guid? deletedBy)
    {
        IsDeleted = true;
        DeletedOn = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
