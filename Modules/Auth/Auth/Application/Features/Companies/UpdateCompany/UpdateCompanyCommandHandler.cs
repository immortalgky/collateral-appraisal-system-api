using Auth.Domain.Companies;
using Shared.Exceptions;

namespace Auth.Application.Features.Companies.UpdateCompany;

public class UpdateCompanyCommandHandler(ICompanyRepository companyRepository)
    : ICommandHandler<UpdateCompanyCommand, UpdateCompanyResult>
{
    public async Task<UpdateCompanyResult> Handle(
        UpdateCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Company", command.Id);

        company.Update(
            command.Name,
            command.TaxId,
            command.Phone,
            command.Email,
            command.Street,
            command.City,
            command.Province,
            command.PostalCode,
            command.ContactPerson,
            command.IsActive,
            command.HostCompanyCode,
            command.LoanTypes);

        // Full-record PUT: bank account is always (re)applied. Callers send the current
        // values back (and null to intentionally clear) — do NOT guard on "provided",
        // that would break intentional clearing.
        company.SetBankAccount(command.BankAccountNo, command.BankAccountName);

        await companyRepository.SaveChangesAsync(cancellationToken);

        return new UpdateCompanyResult(true);
    }
}
