namespace Appraisal.Application.Features.Appraisals.GetPreviousAppraisalChain;

public record GetPreviousAppraisalChainResult(IReadOnlyList<PreviousAppraisalDto> Items);

public record PreviousAppraisalDto(
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime? AppraisalDate,
    decimal? AppraisalValue,
    string? Status,
    int Depth);
