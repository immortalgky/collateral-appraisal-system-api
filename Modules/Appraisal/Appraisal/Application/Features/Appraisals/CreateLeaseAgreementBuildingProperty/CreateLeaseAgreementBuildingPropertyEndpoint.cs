namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementBuildingProperty;

public class CreateLeaseAgreementBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/lease-agreement-building-properties",
                async (
                    Guid appraisalId,
                    Guid? groupId,
                    CreateLeaseAgreementBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateLeaseAgreementBuildingPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId,
                            GroupId = groupId
                        };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateLeaseAgreementBuildingPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/building-detail",
                        response);
                }
            )
            .WithName("CreateLeaseAgreementBuildingProperty")
            .Produces<CreateLeaseAgreementBuildingPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a lease agreement building property")
            .WithDescription("Creates a new lease agreement building property with building detail, lease agreement, and rental info.")
            .WithTags("Appraisal Properties");
    }
}
