using Shared.Data;
using Shared.Pagination;

namespace Request.Application.Features.RequestTitles.GetRequestTitlesByRequestId;

internal class GetRequestTitlesByRequestIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestTitlesByRequestIdQuery, GetRequestTitlesByRequestIdResult>
{
    public async Task<GetRequestTitlesByRequestIdResult> Handle(GetRequestTitlesByRequestIdQuery query,
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
                [NumberOfMachinery] AS NumberOfMachine,
                [BuildingType],
                [UsableArea],
                [NumberOfBuilding],
                [TitleAddress_SubDistrict] AS [TitleAddress.SubDistrict],
                [TitleAddress_District] AS [TitleAddress.District],
                [TitleAddress_Province] AS [TitleAddress.Province],
                [TitleAddress_PostalCode] AS [TitleAddress.PostalCode],
                [DopaAddress_SubDistrict] AS [DopaAddress.SubDistrict],
                [DopaAddress_District] AS [DopaAddress.District],
                [DopaAddress_Province] AS [DopaAddress.Province],
                [DopaAddress_PostalCode] AS [DopaAddress.PostalCode]
            FROM [request].[RequestTitles]
            WHERE [RequestId] = @RequestId
            ORDER BY [CreatedOn] ASC
            """;

        var requestTitles = await connectionFactory.QueryAsync<RequestTitleDto>(
            sql,
            new { query.RequestId });

        return new GetRequestTitlesByRequestIdResult(requestTitles.ToList());
    }
}