using MediatR;

namespace Auth.Application.Features.Companies.UpdateCompanyBankAccount;

public class UpdateCompanyBankAccountEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/companies/{id:guid}/bank-account",
                async (Guid id, UpdateCompanyBankAccountRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateCompanyBankAccountCommand(id, request.BankAccountNo, request.BankAccountName);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateCompanyBankAccount")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update company bank account details")
            .WithDescription("Admin-only: set or update BankAccountNo and BankAccountName for an appraisal company.")
            .WithTags("Company")
            .RequireAuthorization("CanManageCompanies");
    }
}

public record UpdateCompanyBankAccountRequest(string? BankAccountNo, string? BankAccountName);
