namespace Auth.Application.Features.Companies.CreateCompany;

public record CreateCompanyRequest(
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
    string? HostCompanyCode = null,
    List<string>? LoanTypes = null
);
