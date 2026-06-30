
public record GetCondoPMAPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetCondoPMAPropertyResult>;
