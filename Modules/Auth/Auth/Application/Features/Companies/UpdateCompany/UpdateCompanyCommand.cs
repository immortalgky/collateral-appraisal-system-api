namespace Auth.Application.Features.Companies.UpdateCompany;

public record UpdateCompanyCommand(
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
) : ICommand<UpdateCompanyResult>;
