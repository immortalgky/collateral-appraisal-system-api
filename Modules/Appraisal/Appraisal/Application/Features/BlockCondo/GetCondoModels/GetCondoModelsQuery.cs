namespace Appraisal.Application.Features.BlockCondo.GetCondoModels;

/// <summary>
/// Query to get all condo models for an appraisal
/// </summary>
public record GetCondoModelsQuery(Guid AppraisalId) : IQuery<GetCondoModelsResult>;
