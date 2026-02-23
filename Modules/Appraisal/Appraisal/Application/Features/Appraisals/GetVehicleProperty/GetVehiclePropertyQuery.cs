using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetVehicleProperty;

/// <summary>
/// Query to get a vehicle property with its detail
/// </summary>
public record GetVehiclePropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetVehiclePropertyResult>;
