namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:guid}/titles/{titleId:Guid}/draft", 
            async (Guid requestId, Guid titleId, UpdateDraftRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
            {

                var command = new UpdateDraftRequestTitleCommand(
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
                );

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<UpdateDraftRequestTitleResponse>();

                return Results.Ok(response);
            })
            .WithName("UpdateDraftRequestTitle")
            .Produces<UpdateDraftRequestTitleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update draft a request title")
            .WithDescription(
                "Updates draft an existing title/collateral for the specified request. All title details including land area, building information, vehicle, and machine details can be modified.")
            .WithTags("Request Titles")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}
