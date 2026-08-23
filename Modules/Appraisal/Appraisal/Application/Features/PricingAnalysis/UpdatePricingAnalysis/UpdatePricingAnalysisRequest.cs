namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;

/// <summary>
/// Request to update a pricing analysis. Only <see cref="AppraisedValue"/> and
/// <see cref="UseSystemCalc"/> are honoured, and only when non-null — so a caller that just wants
/// to flip the calculation mode sends <c>{ "useSystemCalc": false }</c> and the final value the
/// rollup computed is left alone.
/// <para>
/// <see cref="MarketValue"/> and <see cref="ForcedSaleValue"/> are accepted but discarded: the
/// aggregate only carries <c>FinalAppraisedValue</c>. They are kept so the existing wire shape
/// does not change; do not add callers that rely on them.
/// </para>
/// </summary>
public record UpdatePricingAnalysisRequest(
    decimal? MarketValue,
    decimal? AppraisedValue,
    decimal? ForcedSaleValue,
    bool? UseSystemCalc
);
