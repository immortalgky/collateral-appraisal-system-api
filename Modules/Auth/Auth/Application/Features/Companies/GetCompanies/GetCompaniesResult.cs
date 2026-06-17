namespace Auth.Application.Features.Companies.GetCompanies;

public record GetCompaniesResult(List<CompanyDto> Companies);

public record CompanyDto(
    Guid         Id,
    string       Name,
    string?      NameLocal,
    string?      TaxId,
    string?      Phone,
    string?      Email,
    string?      AddressLine1,
    string?      AddressLine2,
    DateTime?    EffectiveDate,
    DateTime?    ExpireDate,
    string?      ContactPerson,
    bool         IsActive,
    List<string> LoanTypes,
    string?      HostCompanyCode  = null,
    string?      BankAccountNo    = null,
    string?      BankAccountName  = null,
    decimal      AverageRating    = 0m,
    int          EvaluationCount  = 0,
    int          ActiveAssignments = 0,
    int          UserCount        = 0);
