using Shared.Data;
using Shared.Pagination;

namespace Request.Application.Features.RequestTitles.GetRequestTitleById;

internal class GetRequestTitleByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestTitleByIdQuery, GetRequestTitleByIdResult>
{
    public async Task<GetRequestTitleByIdResult> Handle(GetRequestTitleByIdQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                [Id],
                [RequestId],
                [CollateralType],
                [CollateralStatus],
                [TitleNo],
                [DeedType],
                [TitleDetail],
                [Rawang],
                [LandNo],
                [SurveyNo],
                [AreaRai],
                [AreaNgan],
                [AreaSquareWa],
                [OwnerName],
                [RegistrationNo],
                [VehicleType],
                [VehicleAppointmentLocation],
                [ChassisNumber],
                [MachineStatus],
                [MachineType],
                [InstallationStatus],
                [InvoiceNumber],
                [NumberOfMachinery],
                [BuildingType],
                [UsableArea],
                [NumberOfBuilding]
            FROM [request].[RequestTitles]
            WHERE [Id] = @Id AND [RequestId] = @RequestId
            """;

        var result = await connectionFactory.QueryFirstOrDefaultAsync<GetRequestTitleByIdResult>(
            sql,
            new { query.Id, query.RequestId });

        if (result is null)
            throw new RequestTitleNotFoundException(query.Id);

        return result;
    }
}
