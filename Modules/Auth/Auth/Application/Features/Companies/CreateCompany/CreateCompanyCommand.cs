namespace Auth.Application.Features.Companies.CreateCompany;

public record CreateCompanyCommand(
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
) : ICommand<CreateCompanyResult>;
