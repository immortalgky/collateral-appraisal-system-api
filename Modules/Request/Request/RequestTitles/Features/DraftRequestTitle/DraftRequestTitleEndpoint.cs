using Request.Extensions;

namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:Guid}/titles/draft", 
                async (Guid requestId, DraftRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new AddRequestTitlesCommand(
                        requestId,
                        request.RequestTitles.Select(rt => new RequestTitlesCommandDto(
                            rt.CollateralType,
                            rt.CollateralStatus,
                            new TitleDeedInfoDto(rt.TitleNo, rt.DeedType, rt.TitleDetail),
                            new SurveyInfoDto(rt.Rawang, rt.LandNo, rt.SurveyNo),
                            new LandAreaDto(rt.AreaRai, rt.AreaNgan, rt.AreaSquareWa),
                            rt.OwnerName,
                            rt.RegistrationNumber,
                            new VehicleDto(rt.VehicleType, rt.VehicleAppointmentLocation, rt.ChassisNumber),
                            new MachineDto(rt.MachineryStatus, rt.MachineryType, rt.InstallationStatus, rt.InvoiceNumber, rt.NumberOfMachinery),
                            new BuildingInfoDto(rt.BuildingType, rt.UsableArea, rt.NumberOfBuilding),
                            new CondoInfoDto(rt.CondoName, rt.BuildingNo, rt.RoomNo, rt.FloorNo),
                            rt.TitleAddress,
                            rt.DopaAddress,
                            rt.Notes,
                            rt.RequestTitleDocuments.Select(rtd => new RequestTitleDocumentDto(rtd.DocumentId, rtd.DocumentType, rtd.IsRequired, rtd.DocumentDescription, rtd.UploadedBy, rtd.UploadedByName)).ToList()
                        )).ToList()
                    );;

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DraftRequestTitleResponse>();

                    return Results.Created($"/requests/{requestId}", response);
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
