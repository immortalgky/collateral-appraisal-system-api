using Auth.Domain.Companies;
using Shared.Exceptions;
using Shared.Identity;

namespace Auth.Application.Features.Companies.DeleteCompany;

public class DeleteCompanyCommandHandler(
    ICompanyRepository companyRepository,
    ICurrentUserService currentUserService)
    : ICommandHandler<DeleteCompanyCommand, DeleteCompanyResult>
{
    public async Task<DeleteCompanyResult> Handle(
        DeleteCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Company", command.Id);

        company.Delete(currentUserService.UserId);

        await companyRepository.SaveChangesAsync(cancellationToken);

        return new DeleteCompanyResult(true);
    }
}
