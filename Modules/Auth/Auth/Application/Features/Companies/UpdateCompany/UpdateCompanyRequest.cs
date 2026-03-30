namespace Auth.Application.Features.Companies.UpdateCompany;

public record UpdateCompanyRequest(
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Street,
    string? City,
    string? Province,
    string? PostalCode,
    string? ContactPerson,
    bool IsActive,
    List<string>? LoanTypes = null
);
