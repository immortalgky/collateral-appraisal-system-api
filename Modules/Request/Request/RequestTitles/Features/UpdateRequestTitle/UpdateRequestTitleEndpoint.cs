namespace Request.RequestTitles.Features.UpdateRequestTitle;

public class UpdateRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:Guid}/titles/{titleId:Guid}",
            async (Guid requestId, Guid titleId, UpdateRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new UpdateRequestTitleCommand(
                    titleId,
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

                var response = result.Adapt<UpdateRequestTitleResponse>();

                return Results.Ok(response);
            })
            .WithName("UpdateRequestTitle")
            .Produces<UpdateRequestTitleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a request title")
            .WithDescription(
                "Updates an existing title/collateral for the specified request. All title details including land area, building information, vehicle, and machine details can be modified.")
            .WithTags("Request Titles")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}