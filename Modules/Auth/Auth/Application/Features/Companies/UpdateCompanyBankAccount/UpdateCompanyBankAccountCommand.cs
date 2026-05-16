using Shared.CQRS;

namespace Auth.Application.Features.Companies.UpdateCompanyBankAccount;

public record UpdateCompanyBankAccountCommand(
    Guid CompanyId,
    string? BankAccountNo,
    string? BankAccountName
) : ICommand<Unit>;
