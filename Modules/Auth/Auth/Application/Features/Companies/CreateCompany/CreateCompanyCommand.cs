namespace Auth.Application.Features.Companies.CreateCompany;

public record CreateCompanyCommand(
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Street,
    string? City,
    string? Province,
    string? PostalCode,
    string? ContactPerson,
    string? HostCompanyCode = null,
    List<string>? LoanTypes = null
) : ICommand<CreateCompanyResult>;
