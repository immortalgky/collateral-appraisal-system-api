namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandProperty;

public class CreateLeaseAgreementLandPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/lease-agreement-land-properties",
                async (
                    Guid appraisalId,
                    Guid? groupId,
                    CreateLeaseAgreementLandPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateLeaseAgreementLandPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId,
                            GroupId = groupId
                        };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateLeaseAgreementLandPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/land-detail",
                        response);
                }
            )
            .WithName("CreateLeaseAgreementLandProperty")
            .Produces<CreateLeaseAgreementLandPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a lease agreement land property")
            .WithDescription("Creates a new lease agreement land property with land detail, lease agreement, and rental info.")
            .WithTags("Appraisal Properties");
    }
}
