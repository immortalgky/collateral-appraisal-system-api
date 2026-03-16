namespace Auth.Application.Features.Companies.GetCompanyById;

public record GetCompanyByIdQuery(Guid Id) : IQuery<GetCompanyByIdResult>;
