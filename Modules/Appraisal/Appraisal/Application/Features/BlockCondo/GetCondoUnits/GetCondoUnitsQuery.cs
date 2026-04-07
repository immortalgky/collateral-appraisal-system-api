namespace Appraisal.Application.Features.BlockCondo.GetCondoUnits;

public record GetCondoUnitsQuery(Guid AppraisalId) : IQuery<GetCondoUnitsResult>;
