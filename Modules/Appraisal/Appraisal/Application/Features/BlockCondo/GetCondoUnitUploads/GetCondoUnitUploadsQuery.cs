namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitUploads;

public record GetCondoUnitUploadsQuery(Guid AppraisalId) : IQuery<GetCondoUnitUploadsResult>;
