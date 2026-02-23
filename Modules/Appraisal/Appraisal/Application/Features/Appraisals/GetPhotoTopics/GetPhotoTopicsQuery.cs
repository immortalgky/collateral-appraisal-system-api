namespace Appraisal.Application.Features.Appraisals.GetPhotoTopics;

public record GetPhotoTopicsQuery(Guid AppraisalId) : IQuery<GetPhotoTopicsResult>;
