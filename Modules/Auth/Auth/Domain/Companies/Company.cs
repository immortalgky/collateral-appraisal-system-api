using Shared.DDD;
using Shared.Exceptions;

namespace Auth.Domain.Companies;

public class Company : Entity<Guid>
{
    public string Name { get; private set; } = default!;
    public string? NameLocal { get; private set; }
    public string? TaxId { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public DateTime? EffectiveDate { get; private set; }
    public DateTime? ExpireDate { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? HostCompanyCode { get; private set; }
    public string? LegacyCompanyCode { get; private set; }
    public string? BankAccountNo { get; private set; }
    public string? BankAccountName { get; private set; }
    public List<string> LoanTypes { get; private set; } = [];
    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedOn { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Company() { }

    public static Company Create(
        string name,
        string? nameLocal = null,
        string? taxId = null,
        string? phone = null,
        string? email = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        DateTime? effectiveDate = null,
        DateTime? expireDate = null,
        string? contactPerson = null,
        string? hostCompanyCode = null,
        List<string>? loanTypes = null,
        string? legacyCompanyCode = null,
        bool isActive = true)
    {
        GuardApprovalWindow(effectiveDate, expireDate);

        return new Company
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NameLocal = nameLocal,
            TaxId = taxId,
            Phone = phone,
            Email = email,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            EffectiveDate = effectiveDate,
            ExpireDate = expireDate,
            ContactPerson = contactPerson,
            HostCompanyCode = hostCompanyCode,
            LegacyCompanyCode = legacyCompanyCode,
            LoanTypes = loanTypes ?? [],
            IsActive = isActive,
            IsDeleted = false
        };
    }

    public void Update(
        string name,
        string? nameLocal,
        string? taxId,
        string? phone,
        string? email,
        string? addressLine1,
        string? addressLine2,
        DateTime? effectiveDate,
        DateTime? expireDate,
        string? contactPerson,
        bool isActive,
        string? hostCompanyCode = null,
        List<string>? loanTypes = null)
    {
        GuardApprovalWindow(effectiveDate, expireDate);

        Name = name;
        NameLocal = nameLocal;
        TaxId = taxId;
        Phone = phone;
        Email = email;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        EffectiveDate = effectiveDate;
        ExpireDate = expireDate;
        ContactPerson = contactPerson;
        HostCompanyCode = hostCompanyCode;
        LoanTypes = loanTypes ?? [];
        IsActive = isActive;
    }

    public void SetBankAccount(string? bankAccountNo, string? bankAccountName)
    {
        BankAccountNo = bankAccountNo;
        BankAccountName = bankAccountName;
    }

    /// <summary>
    /// Whether the company may receive appraisal assignments as of <paramref name="asOf"/>: it must be
    /// active, not deleted, and within its MOU approval window (a null bound is open-ended). Compared
    /// date-only so a company is eligible through the whole of its EffectiveDate/ExpireDate day.
    /// Used by every assignment surface (round-robin, eligible-company picker) so the rule lives once.
    /// </summary>
    public bool IsAssignable(DateTime asOf)
    {
        if (!IsActive || IsDeleted) return false;

        var onDate = asOf.Date;
        if (EffectiveDate is { } effective && effective.Date > onDate) return false;
        if (ExpireDate is { } expire && expire.Date < onDate) return false;
        return true;
    }

    // The MOU approval window must not be inverted. Enforced here (not just in the command validators)
    // so every writer — the API, the seeder, future imports — upholds it; an inverted window would
    // make the company permanently non-IsAssignable with no error.
    private static void GuardApprovalWindow(DateTime? effectiveDate, DateTime? expireDate)
    {
        // Compared date-only to match IsAssignable (which evaluates the window by calendar day), so a
        // legitimate same-day window is not rejected over a difference in time-of-day.
        if (effectiveDate is { } eff && expireDate is { } exp && eff.Date > exp.Date)
            throw new DomainException("Effective date must be on or before expire date.");
    }

    public void Delete(Guid? deletedBy)
    {
        IsDeleted = true;
        DeletedOn = DateTime.Now;
        DeletedBy = deletedBy;
    }
}
