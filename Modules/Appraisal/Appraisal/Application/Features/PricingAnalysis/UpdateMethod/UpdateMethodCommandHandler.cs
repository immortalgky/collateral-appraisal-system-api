using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

/// <summary>
/// Handler for updating a method
/// </summary>
public class UpdateMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdateMethodCommand, UpdateMethodResult>
{
    public async Task<UpdateMethodResult> Handle(
        UpdateMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new InvalidOperationException($"Pricing analysis with ID '{command.PricingAnalysisId}' not found");

        PricingAnalysisMethod? method = null;
        PricingAnalysisApproach? parentApproach = null;

        foreach (var approach in pricingAnalysis.Approaches)
        {
            method = approach.Methods.FirstOrDefault(m => m.Id == command.MethodId);
            if (method != null)
            {
                parentApproach = approach;
                break;
            }
        }

        if (method == null)
            throw new InvalidOperationException($"Method with ID '{command.MethodId}' not found");

        // Captured before ANYTHING in this handler touches the method — in particular before
        // SetMethodCalcMode below, whose ClearValue() nulls MethodValue. Read after that, every
        // request that flips the calc mode while carrying a MethodValue (the board sends one on
        // every save) compared against null and so always looked like an edit, stamping
        // IndicatedValue on a Role=Land method the appraiser never typed into.
        var methodValueBeforeThisSave = method.MethodValue;

        // UseSystemCalc: null leaves it unchanged (same convention as Remark below). Applied BEFORE
        // SetValue: a flip clears the value, so a MethodValue sent in the same request must land
        // after it. SetCalcMode is the single domain operation for a flip — flag + unselect + clear value + drop manual land
        // area — see its remarks on PricingAnalysisMethod for why that bundle lives in the
        // aggregate rather than here. Called through the aggregate root so the deselection also
        // clears the approach/final figure it was carrying (RecalculateRollup below would keep it).
        if (command.UseSystemCalc.HasValue)
        {
            pricingAnalysis.SetMethodCalcMode(method.Id, command.UseSystemCalc.Value);
        }

        // Only call SetValue if at least one parameter is provided
        if (command.MethodValue.HasValue || command.ValuePerUnit.HasValue || command.UnitType != null)
        {
            method.SetValue(
                command.MethodValue ?? method.MethodValue ?? 0,
                command.ValuePerUnit ?? method.ValuePerUnit,
                command.UnitType ?? method.UnitType);
        }

        // Remark has its own condition: null means "not provided, leave unchanged"; empty
        // string means "clear it" (mirrors the not-provided semantics UnitType already has
        // above — there is no separate sentinel type for clearing).
        if (command.Remark != null)
        {
            method.SetRemark(command.Remark.Length == 0 ? null : command.Remark);
        }

        // Role: null leaves it unchanged (same convention as Remark and UseSystemCalc above).
        // Through the approach, which rejects a role that would count a Cost component twice.
        if (command.Role != null)
        {
            parentApproach!.SetMethodRole(method.Id, command.Role);
        }

        // A price typed into the board's own box is the appraiser's figure for this method, and on a
        // Role=Land method that figure IS the land price — so record it as IndicatedValue and let
        // the domain carry it into LandValue. Without this the board wrote MethodValue alone: the
        // row kept an older IndicatedValue, which the next save pushed back over MethodValue through
        // SyncMethodValueWithIndicatedValue, quietly undoing what was typed here.
        //
        // Scoped to Role=Land on purpose. Writing IndicatedValue for every method would stamp it on
        // rows where the appraiser typed nothing — the column means "they typed over the total", and
        // re-creating that is what 20260922090000_DataFix_ClearStampedLeaseholdProfitRentIndicatedValue.sql
        // exists to undo. On a Role=Land method the manual-cost path already writes it on every save.
        //
        // Gated on the value having actually MOVED, not merely on it being present. The board's save
        // loop sends MethodValue for any method it considers dirty — including one where only the
        // remark changed — so stamping on presence alone meant typing a note froze the price: an
        // IndicatedValue that was previously null starts pinning MethodValue through
        // SyncMethodValueWithIndicatedValue, and later recalculations from the comparables stop
        // moving it. Exactly what the data-fix script named below exists to undo.
        //
        // NOT closed by this gate: a STALE board. If the method was recalculated elsewhere after the
        // board loaded, the value it echoes back differs from the server's and reads as an edit, so
        // the old figure is both written and stamped. That save already clobbers MethodValue today
        // (SetValue above does it regardless) — this only makes it stickier. Closing it properly
        // needs the request to say whether the appraiser actually typed the number, which is a
        // change to the command contract and to the board's save loop, not something this gate can
        // infer.
        //
        // Role is read AFTER SetMethodRole above, so a request that sets both lands on the new role.
        // Runs last, after everything that writes LandValue — see the method's own remarks.
        // Never on a save that switches system calc back ON. That request means "recompute this from
        // the comparables", which is the opposite of "the appraiser typed a figure" — and SetCalcMode
        // has already cleared MethodValue, so any value riding along compares against null and reads
        // as an edit. Stamping there froze the method at a number nobody typed, on the very save that
        // asked for it to stop being frozen.
        // No prior value counts as MOVED. A method priced for the first time on the board starts at
        // null, and demanding a previous figure made that first price unrecordable — then
        // unrecordable for good, because the next save compares against the now-equal value. The
        // lifted != is deliberate (null != x is true); what makes it safe is the manual-mode check
        // below, which is what actually separates "the appraiser typed this" from "the board echoed
        // what it loaded".
        // !method.UseSystemCalc, not merely "the request did not switch it on": the flag defaults to
        // TRUE and the command leaves it unchanged when null, so an ordinary remark save on a
        // system-calculated method passed the previous check. A typed board figure only means
        // anything on a method in MANUAL mode; on a calculated one the number is whatever the board
        // last loaded, and stamping it pins the price against every future recalculation.
        if (!method.UseSystemCalc
            && command.UseSystemCalc != true
            && command.MethodValue.HasValue
            && command.MethodValue.Value != methodValueBeforeThisSave
            && method.FinalValue is not null
            && string.Equals(method.Role, "Land", StringComparison.OrdinalIgnoreCase))
        {
            method.FinalValue.SetIndicatedValue(command.MethodValue.Value);
            method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);
        }

        // Roll the new method value up through approach → analysis (null-safe, idempotent).
        pricingAnalysis.RecalculateRollup();

        return new UpdateMethodResult(
            method.Id,
            method.MethodType,
            method.MethodValue,
            method.ValuePerUnit,
            method.UnitType,
            method.Remark,
            method.UseSystemCalc,
            parentApproach!.ApproachValue,
            pricingAnalysis.FinalAppraisedValue);
    }
}
