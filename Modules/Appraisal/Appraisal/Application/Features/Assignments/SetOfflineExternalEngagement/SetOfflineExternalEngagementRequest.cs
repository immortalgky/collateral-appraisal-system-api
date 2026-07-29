namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

/// <summary>
/// No AssignedBy: the actor is resolved from the authenticated principal in the endpoint.
/// A body-supplied actor is unverifiable and this row feeds fee and audit surfaces.
/// </summary>
public record SetOfflineExternalEngagementRequest(
    Guid CompanyId,
    /// <summary>Appraisal date printed on the external company's book.</summary>
    DateTime BookDate,
    string? ExternalAppraiserName = null);
