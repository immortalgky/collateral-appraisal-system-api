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
            command.TaxId,
            command.Phone,
            command.Email,
            command.Street,
            command.City,
            command.Province,
            command.PostalCode,
            command.ContactPerson,
            command.LoanTypes);

        await companyRepository.AddAsync(company, cancellationToken);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return new CreateCompanyResult(company.Id);
    }
}
