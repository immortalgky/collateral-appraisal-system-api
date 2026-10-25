using System.Text.Json.Serialization;

namespace Appraisal.Application.Features.Fees.GetFeePreview;

// The API drops null properties app-wide; this response is agreed with the front end as always carrying every
// key ("null when absent"), so the nullable ones are pinned to be written.

/// <param name="FeeId">The fee row of the current assignment; null when none exists yet (the first admin add creates it).</param>
/// <param name="SystemBaseAmount">What the system would charge as the base appraisal fee (ex-VAT); null when none can be resolved.</param>
/// <param name="AdminOverrideAmount">The admin's own base (FeeCode 01) amount; null when the admin has not set one.</param>
/// <param name="Items">The fee's existing items plus the would-be system base item (Id null) when it is not overridden.</param>
/// <param name="HasBillableBase">Would assignment bill a base (FeeCode 01) above 0: an Admin or System 01, or a User 01 that is billable (not pending or rejected). The one rule Assign enforces (FeeBaseMissing) and the screen should show.</param>
/// <param name="Subtotal">Ex-VAT sum of the billable items (not awaiting approval, not rejected).</param>
public record FeePreviewResult(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? FeeId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? SystemBaseAmount,
    FeeBaseSourceDto BaseSource,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? AdminOverrideAmount,
    List<FeePreviewItemDto> Items,
    bool HasBillableBase,
    decimal Subtotal,
    decimal VatRate,
    decimal Vat,
    decimal Total);

/// <param name="Kind">One of <see cref="Services.FeeBaseKinds"/>.</param>
public record FeeBaseSourceDto(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AppraisalNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? QuotationNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? WinnerCompanyName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? SellingPrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? TierMin,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? TierMax);

/// <param name="Source">System | Admin | User (<see cref="Domain.Appraisals.FeeItemSource"/>).</param>
public record FeePreviewItemDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? Id,
    string FeeCode,
    string Description,
    decimal Amount,
    string Source);
