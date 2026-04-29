namespace Auth.Application.Features.Companies.GetEligibleCompanies;

public class GetEligibleCompaniesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/companies/eligible", async (
                [FromQuery] string? loanType,
                ISender sender) =>
            {
                var result = await sender.Send(new GetEligibleCompaniesQuery(loanType));
                return Results.Ok(result);
            })
            .WithName("GetEligibleCompanies")
            .Produces<GetEligibleCompaniesResult>()
            .WithSummary("Get Eligible Companies")
            .WithDescription("Get active companies that accept the specified loan type");
    }
}
