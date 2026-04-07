namespace Appraisal.Application.Features.BlockCondo.GetCondoTowers;

public record GetCondoTowersQuery(Guid AppraisalId) : IQuery<GetCondoTowersResult>;
