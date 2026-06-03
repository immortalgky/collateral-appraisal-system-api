namespace Auth.Application.Features.Companies.GetCompanies;

public record GetCompaniesResult(List<CompanyDto> Companies);

public record CompanyDto(
    Guid         Id,
    string       Name,
    string?      TaxId,
    string?      Phone,
    string?      Email,
    string?      Street,
    string?      City,
    string?      Province,
    string?      PostalCode,
    string?      ContactPerson,
    bool         IsActive,
    List<string> LoanTypes,
    decimal      AverageRating    = 0m,
    int          EvaluationCount  = 0,
    int          ActiveAssignments = 0);
