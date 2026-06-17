namespace Auth.Application.Features.Companies.UpdateCompany;

public record UpdateCompanyCommand(
    Guid Id,
    string Name,
    string? NameLocal,
    string? TaxId,
    string? Phone,
    string? Email,
    string? AddressLine1,
    string? AddressLine2,
    DateTime? EffectiveDate,
    DateTime? ExpireDate,
    string? ContactPerson,
    bool IsActive,
    string? HostCompanyCode = null,
    List<string>? LoanTypes = null,
    string? BankAccountNo = null,
    string? BankAccountName = null
) : ICommand<UpdateCompanyResult>;
