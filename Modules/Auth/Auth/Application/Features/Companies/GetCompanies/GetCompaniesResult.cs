namespace Auth.Application.Features.Companies.GetCompanies;

public record GetCompaniesResult(List<CompanyDto> Companies);

public record CompanyDto(
    Guid Id,
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Street,
    string? City,
    string? Province,
    string? PostalCode,
    bool IsActive
);
