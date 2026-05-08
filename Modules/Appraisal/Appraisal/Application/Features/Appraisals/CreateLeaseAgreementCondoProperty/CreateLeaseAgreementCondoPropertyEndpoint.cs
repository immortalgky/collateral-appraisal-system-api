namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;

public class CreateLeaseAgreementCondoPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/lease-agreement-condo-properties",
                async (
                    Guid appraisalId,
                    Guid? groupId,
                    CreateLeaseAgreementCondoPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateLeaseAgreementCondoPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId,
                            GroupId = groupId
                        };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateLeaseAgreementCondoPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/lease-agreement-condo-detail",
                        response);
                }
            )
            .WithName("CreateLeaseAgreementCondoProperty")
            .Produces<CreateLeaseAgreementCondoPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a lease agreement condo property")
            .WithDescription("Creates a new lease agreement condo property with condo detail, lease agreement, and rental info.")
            .WithTags("Appraisal Properties");
    }
}
