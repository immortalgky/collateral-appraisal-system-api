namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

/// <summary>
/// No AssignedBy: a body-supplied actor is unverifiable and this row feeds fee and audit surfaces; the handler
/// keeps the admin already recorded on the row, else uses the authenticated principal.
/// </summary>
public record SetOfflineExternalEngagementRequest(
    Guid CompanyId,
    /// <summary>Appraisal date printed on the external company's book.</summary>
    DateTime BookDate,
    string? ExternalAppraiserName = null);
