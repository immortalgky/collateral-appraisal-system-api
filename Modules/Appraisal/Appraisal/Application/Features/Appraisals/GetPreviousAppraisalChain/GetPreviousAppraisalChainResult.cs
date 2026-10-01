namespace Appraisal.Application.Features.Appraisals.GetPreviousAppraisalChain;

public record GetPreviousAppraisalChainResult(IReadOnlyList<PreviousAppraisalDto> Items);

/// <summary>
/// One ancestor. <see cref="AppraisalId"/> is NULL only for the last item of a chain that reaches back
/// past CAS to a legacy AS400 book ("99A…"): it has a number, value and date but nothing to open.
/// </summary>
public record PreviousAppraisalDto(
    Guid? AppraisalId,
    string AppraisalNumber,
    DateTime? AppraisalDate,
    decimal? AppraisalValue,
    string? Status,
    int Depth);
