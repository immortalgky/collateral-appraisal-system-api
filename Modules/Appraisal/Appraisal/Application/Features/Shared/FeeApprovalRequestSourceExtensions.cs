using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Shared;

/// <summary>
/// Single place that maps the acting user to a fee/appointment approval
/// <see cref="FeeApprovalRequestSource"/>. Keeping this in one helper ensures edit-time and
/// submit-time derivations cannot drift — a mismatch would make
/// <c>RaiseFeeAppointmentApprovalCommandHandler</c> throw "no tier matched".
/// External valuation-company callers carry a <c>company_id</c> claim; bank-internal callers do not.
/// </summary>
public static class FeeApprovalRequestSourceExtensions
{
    public static string ToFeeApprovalRequestSource(this ICurrentUserService user) =>
        user.IsExternal ? FeeApprovalRequestSource.External : FeeApprovalRequestSource.Internal;
}
