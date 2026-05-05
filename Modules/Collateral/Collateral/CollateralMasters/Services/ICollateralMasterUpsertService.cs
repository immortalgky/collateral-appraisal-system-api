namespace Collateral.CollateralMasters.Services;

/// <summary>
/// Single entry point for creating/updating CollateralMasters from a completed appraisal.
/// Used by the MassTransit consumer, the backfill job, and the admin replay endpoint.
/// </summary>
public interface ICollateralMasterUpsertService
{
    /// <summary>
    /// Processes a completed appraisal: looks up all in-scope properties, finds or creates
    /// a CollateralMaster for each, upserts last-known data, and appends an engagement record.
    /// </summary>
    /// <exception cref="CollateralMasters.Exceptions.MissingIdentityKeyException">
    /// Thrown when a required dedup field is absent — causes MassTransit to dead-letter.
    /// </exception>
    Task ProcessAppraisalAsync(Guid appraisalId, CancellationToken ct = default);
}
