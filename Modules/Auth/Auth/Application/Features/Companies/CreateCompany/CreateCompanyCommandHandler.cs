using Auth.Domain.Companies;

namespace Auth.Application.Features.Companies.CreateCompany;

public class CreateCompanyCommandHandler(ICompanyRepository companyRepository)
    : ICommandHandler<CreateCompanyCommand, CreateCompanyResult>
{
    public async Task<CreateCompanyResult> Handle(
        CreateCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var company = Company.Create(
            command.Name,
            command.NameLocal,
            command.TaxId,
            command.Phone,
            command.Email,
            command.AddressLine1,
            command.AddressLine2,
            command.EffectiveDate,
            command.ExpireDate,
            command.ContactPerson,
            command.HostCompanyCode,
            command.LoanTypes);

        await companyRepository.AddAsync(company, cancellationToken);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return new CreateCompanyResult(company.Id);
    }
}
