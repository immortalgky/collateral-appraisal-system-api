using Auth.Domain.Companies;
using Shared.Exceptions;
using Shared.CQRS;

namespace Auth.Application.Features.Companies.UpdateCompanyBankAccount;

public class UpdateCompanyBankAccountCommandHandler(ICompanyRepository companyRepository)
    : ICommandHandler<UpdateCompanyBankAccountCommand, Unit>
{
    public async Task<Unit> Handle(
        UpdateCompanyBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(command.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", command.CompanyId);

        company.SetBankAccount(command.BankAccountNo, command.BankAccountName);

        await companyRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
