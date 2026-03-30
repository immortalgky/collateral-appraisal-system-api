namespace Auth.Application.Features.Companies.CreateCompany;

public record CreateCompanyRequest(
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Street,
    string? City,
    string? Province,
    string? PostalCode,
    string? ContactPerson,
    List<string>? LoanTypes = null
);
