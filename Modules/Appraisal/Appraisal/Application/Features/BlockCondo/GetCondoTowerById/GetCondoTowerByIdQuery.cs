namespace Appraisal.Application.Features.BlockCondo.GetCondoTowerById;

public record GetCondoTowerByIdQuery(Guid AppraisalId, Guid TowerId) : IQuery<GetCondoTowerByIdResult>;
