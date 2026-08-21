using Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;
using Shared.Result;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValueManual;


public class UpdateFinalValueManualEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/pricing-analysis/{id:guid}/final-values/{methodId:guid}", async (
                    Guid id,
                    Guid methodId,
                    UpdateFinalValueManualRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
        {
            var command = new UpdateFinalValueManualCommand(
                id,
                methodId,
                request.FinalValue,
                request.FinalValueRounded,
                request.IncludeLandArea,
                request.LandArea,
                request.LandValue,
                request.HasBuildingValue,
                request.BuildingValue,
                request.AppraisalPrice
            );

            var response = await sender.Send(command, cancellationToken);

            return Results.Ok(response);
        });
    }
}
public record UpdateFinalValueManualRequest(
    decimal FinalValue,
    decimal FinalValueRounded,
    bool? IncludeLandArea = null,
    decimal? LandArea = null,
    decimal? LandValue = null,
    bool? HasBuildingValue = null,
    decimal? BuildingValue = null,
    decimal? AppraisalPrice = null
);

public record UpdateFinalValueManualCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal FinalValue,
    decimal FinalValueRounded,
    bool? IncludeLandArea,
    decimal? LandArea,
    decimal? LandValue,
    bool? HasBuildingValue,
    decimal? BuildingValue,
    decimal? AppraisalPrice
) : ICommand<UpdateFinalValueManualResult>,
ITransactionalCommand<IAppraisalUnitOfWork>;

public class UpdateFinalValueManualCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<UpdateFinalValueManualCommand, UpdateFinalValueManualResult>
{
    public async Task<UpdateFinalValueManualResult> Handle(UpdateFinalValueManualCommand command, CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new NotFoundException("PricingAnalysisMethod", command.MethodId);

        var fv = method.FinalValue;
        if (method.FinalValue is null)
        {
            fv = PricingFinalValue.Create(
                method.Id,
                command.FinalValue,
                command.FinalValueRounded
            );
        }

        if (command.LandValue is not null)
            fv.UpdateFinalValue(command.LandValue.Value, command.LandValue.Value);

        return new UpdateFinalValueManualResult(true);
    }
}

public record UpdateFinalValueManualResult(bool IsSuccess);