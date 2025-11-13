using Request.Extensions;

namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:Guid}/titles/draft", 
                async (Guid requestId, DraftRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DraftRequestTitleCommand(
                        requestId,
                        request.CollateralType,
                        request.CollateralStatus,
                        new TitleDeedInfoDto(request.TitleNo, request.DeedType, request.TitleDetail),
                        new SurveyInfoDto(request.Rawang, request.LandNo, request.SurveyNo),new LandAreaDto(request.AreaRai, request.AreaNgan, request.AreaSquareWa),
                        request.OwnerName,
                        request.RegistrationNumber,
                        new VehicleDto(request.VehicleType, request.VehicleAppointmentLocation, request.ChassisNumber),
                        new MachineryDto(request.MachineryStatus, request.MachineryType, request.InstallationStatus, request.InvoiceNumber, request.NumberOfMachinery),
                        new BuildingInfoDto(request.BuildingType, request.UsableArea, request.NumberOfBuilding),
                        new CondoInfoDto(request.CondoName, request.BuildingNo, request.RoomNo, request.FloorNo),
                        request.TitleAddress,
                        request.DopaAddress,
                        request.Notes
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DraftRequestTitleResponse>();

                    return Results.Created($"/requests/{requestId}/titles/{response.Id}", response.Id);
                })
            .WithName("DraftRequestTitle")
            .Produces<DraftRequestTitleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Draft a new request title")
            .WithDescription(
                "Draft a new title/collateral for the specified request. The title details including land area, building information, vehicle, and machine details are provided in the request body.")
            .WithTags("Request Titles")
            .AllowAnonymous();
            // .RequireAuthorization("CanWriteRequest");
  }
}
