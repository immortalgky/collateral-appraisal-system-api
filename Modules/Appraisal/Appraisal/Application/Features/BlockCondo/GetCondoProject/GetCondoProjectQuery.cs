namespace Appraisal.Application.Features.BlockCondo.GetCondoProject;

/// <summary>
/// Query to get the condo project for an appraisal
/// </summary>
public record GetCondoProjectQuery(Guid AppraisalId) : IQuery<GetCondoProjectResult>;
