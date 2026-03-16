namespace Auth.Application.Features.Companies.DeleteCompany;

public record DeleteCompanyCommand(Guid Id) : ICommand<DeleteCompanyResult>;
