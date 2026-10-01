using System.Text.Json;
using Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateMachineryProperty;
using Appraisal.Application.Features.Appraisals.UpdateVehicleProperty;
using Appraisal.Application.Features.Appraisals.UpdateVesselProperty;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>What a target needs to bind the request body and write it into the property.</summary>
internal sealed record CorrectionInput(
    AppraisalProperty Property,
    JsonElement Data,
    JsonSerializerOptions Json,
    ISender Mediator,
    CancellationToken Ct);

/// <param name="Type">The one property type this suffix is for.</param>
/// <param name="Apply">Binds <see cref="CorrectionInput.Data"/> like the real PUT does and runs its applier.</param>
internal sealed record PropertyCorrectionTarget(PropertyType Type, Func<CorrectionInput, Task> Apply);

/// <summary>
/// The correctable properties, keyed by the suffix of the real PUT route. Each target binds the body to the
/// same request type the real endpoint takes, adapts it to the same command, and hands it to the same
/// applier the real handler uses — so the correction can never accept or write anything the page could not.
///
/// A suffix only fits its own type. The real land handler would also accept a lease-agreement land property,
/// but its command has no lease or rental section and it clears both unless IsRentedOut is set — on a lease
/// property that would wipe the lease.
///
/// What the real handlers do besides applying the payload is deliberately not here: no valuation
/// recompute (approved figures stay as approved) and no LOS push. Derived values kept per property: the condo
/// fire-insurance price, only when its inputs changed, and a building's stored insurance, which the shared
/// applier re-resolves from its depreciation rows unless a figure was typed.
/// </summary>
internal static class PropertyCorrectionTargets
{
    private static readonly IReadOnlyDictionary<string, PropertyCorrectionTarget> BySuffix =
        new Dictionary<string, PropertyCorrectionTarget>
        {
            ["land-detail"] = Simple<UpdateLandPropertyRequest, UpdateLandPropertyCommand>(
                PropertyType.Land, LandPropertyApplier.Apply),
            ["building-detail"] = Simple<UpdateBuildingPropertyRequest, UpdateBuildingPropertyCommand>(
                PropertyType.Building, BuildingPropertyApplier.Apply),
            ["land-and-building-detail"] = Simple<UpdateLandAndBuildingPropertyRequest, UpdateLandAndBuildingPropertyCommand>(
                PropertyType.LandAndBuilding, LandAndBuildingPropertyApplier.Apply),
            ["condo-detail"] = Condo<UpdateCondoPropertyRequest, UpdateCondoPropertyCommand>(
                PropertyType.Condo, CondoPropertyApplier.Apply, c => (c.FireInsuranceCode, c.UsableArea)),
            ["machinery-detail"] = Simple<UpdateMachineryPropertyRequest, UpdateMachineryPropertyCommand>(
                PropertyType.Machinery, MachineryPropertyApplier.Apply),
            ["vehicle-detail"] = Simple<UpdateVehiclePropertyRequest, UpdateVehiclePropertyCommand>(
                PropertyType.Vehicle, VehiclePropertyApplier.Apply),
            ["vessel-detail"] = Simple<UpdateVesselPropertyRequest, UpdateVesselPropertyCommand>(
                PropertyType.Vessel, VesselPropertyApplier.Apply),
            ["lease-agreement-land-detail"] = Simple<UpdateLeaseAgreementLandPropertyRequest, UpdateLeaseAgreementLandPropertyCommand>(
                PropertyType.LeaseAgreementLand, LeaseAgreementLandPropertyApplier.Apply),
            ["lease-agreement-building-detail"] = Simple<UpdateLeaseAgreementBuildingPropertyRequest, UpdateLeaseAgreementBuildingPropertyCommand>(
                PropertyType.LeaseAgreementBuilding, LeaseAgreementBuildingPropertyApplier.Apply),
            ["lease-agreement-condo-detail"] = Condo<UpdateLeaseAgreementCondoPropertyRequest, UpdateLeaseAgreementCondoPropertyCommand>(
                PropertyType.LeaseAgreementCondo, LeaseAgreementCondoPropertyApplier.Apply, c => (c.FireInsuranceCode, c.UsableArea)),
            ["lease-agreement-land-building-detail"] = Simple<UpdateLeaseAgreementLandAndBuildingPropertyRequest, UpdateLeaseAgreementLandAndBuildingPropertyCommand>(
                PropertyType.LeaseAgreementLandAndBuilding, LeaseAgreementLandAndBuildingPropertyApplier.Apply),
        };

    public static PropertyCorrectionTarget? Find(string suffix) =>
        BySuffix.GetValueOrDefault(suffix);

    private static PropertyCorrectionTarget Simple<TRequest, TCommand>(
        PropertyType type, Action<AppraisalProperty, TCommand> apply)
        where TRequest : class
        =>
            new(type, input =>
            {
                apply(input.Property, Bind<TRequest>(input).Adapt<TCommand>());
                return Task.CompletedTask;
            });

    /// <summary>
    /// The real handler re-derives BuildingInsurancePrice from TODAY's fire-insurance rate on every save.
    /// A correction that never touched the area or the condition must not silently re-price the insurance
    /// with a rate that changed since approval, so the stored value is kept unless one of the two inputs changed.
    /// </summary>
    private static PropertyCorrectionTarget Condo<TRequest, TCommand>(
        PropertyType type,
        Action<AppraisalProperty, TCommand, decimal?> apply,
        Func<TCommand, (string? FireInsuranceCode, decimal? UsableArea)> inputs)
        where TRequest : class
        =>
            new(type, async input =>
            {
                var command = Bind<TRequest>(input).Adapt<TCommand>();
                var (code, area) = inputs(command);
                var condo = input.Property.CondoDetail
                            ?? throw new InvalidOperationException($"Condo detail not found for property {input.Property.Id}");

                var codeChanged = !string.Equals(condo.FireInsuranceCode ?? "", code ?? "", StringComparison.Ordinal);

                var insurancePrice = condo.BuildingInsurancePrice;
                if (codeChanged || condo.UsableArea != area)
                {
                    insurancePrice = await CondoFireInsuranceCalculator.DeriveBuildingInsurancePriceAsync(
                        input.Mediator, code, area, input.Ct);

                    // The real command's validator rejects an unknown condition; that check is not in this
                    // path. With a condition set, a null price means no rate matched it — also when only the
                    // area changed and the stored condition is no longer a rate.
                    if (!string.IsNullOrEmpty(code) && insurancePrice is null)
                        throw new BadRequestException("Fire insurance condition is not a recognized Condo condition.");
                }

                apply(input.Property, command, insurancePrice);
            });

    private static TRequest Bind<TRequest>(CorrectionInput input) where TRequest : class
    {
        try
        {
            return input.Data.Deserialize<TRequest>(input.Json)
                   ?? throw new BadRequestException("'data' must be the JSON object the real page would send.");
        }
        catch (JsonException ex)
        {
            // Same wording as the global handler uses for a malformed body on the real endpoints.
            throw new BadRequestException(
                $"Invalid value at '{ex.Path}'. Please check the data type and format.");
        }
    }
}
