using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetInProgressAppraisalsByCollateral;

/// <summary>
/// Appraisals still in the workflow that hold the collateral identified by the same key the
/// collateral lookup takes (type + dedup columns). Parameter names mirror
/// <c>GET /collateral-masters/lookup</c>.
/// </summary>
public record GetInProgressAppraisalsByCollateralQuery(
    string Type,
    // Land (province, district, subDistrict are shared with Condo)
    string? Province,
    string? District,
    string? SubDistrict,
    string? TitleNumber,
    // Condo
    string? CondoRegistrationNumber,
    string? Building,
    string? Floor,
    string? Unit,
    // Machine
    string? MachineRegistrationNo
) : IQuery<IReadOnlyList<InProgressAppraisalDto>>;

public record InProgressAppraisalDto(Guid AppraisalId, string? AppraisalNumber, string Status);
