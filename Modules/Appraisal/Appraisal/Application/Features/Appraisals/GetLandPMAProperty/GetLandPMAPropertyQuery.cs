public record GetLandPMAPropertyQuery(Guid AppraisalId, Guid PropertyId) : IQuery<GetLandPMAPropertyResult>;
