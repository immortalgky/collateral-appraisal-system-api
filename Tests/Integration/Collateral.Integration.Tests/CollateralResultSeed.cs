using Appraisal.Domain.Appraisals;
using Dapper;
using Shared.Data;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Seeds an appraisal that vw_CollateralResultExport considers in scope: a Completed ReAppraisal whose
/// request has Purpose '03' and that completed on or after the 2026-11-01 cutoff. Each test then varies
/// the one thing it is about.
/// </summary>
internal static class CollateralResultSeed
{
    public static readonly DateTime InScope = new(2026, 11, 5, 10, 0, 0);

    /// <summary>A request row, written the way QuickSearchEndpointTests does (only the columns the view reads).</summary>
    public static async Task<Guid> InsertRequestAsync(
        ISqlConnectionFactory factory, string purpose = "03",
        string? book = null, string? collateralId = null, string? autoUpdate = null)
    {
        var id = Guid.CreateVersion7();
        await factory.GetOpenConnection().ExecuteAsync(
            """
            INSERT INTO request.Requests
                (Id, RequestNumber, Status, Requestor, RequestorName, Creator, CreatorName,
                 IsPma, IsDeleted, CreatedAt, Purpose,
                 ReappraisalBookNumber, ReappraisalCollateralId, ReappraisalAutoUpdate)
            VALUES
                (@Id, @RequestNumber, 'Draft', 'P0000', N'Test', 'P0000', N'Test',
                 0, 0, SYSDATETIME(), @Purpose,
                 @Book, @CollateralId, @AutoUpdate)
            """,
            new
            {
                Id = id,
                RequestNumber = $"REQ-{Guid.NewGuid():N}"[..20],
                Purpose = purpose,
                Book = book,
                CollateralId = collateralId,
                AutoUpdate = autoUpdate,
            });
        return id;
    }

    /// <summary>
    /// A Completed appraisal of the given type. Status and CompletedAt are set by reflection because
    /// the real status flow needs a workflow — the same shortcut AppraisalChainResolutionTests takes.
    /// </summary>
    public static AppraisalAggregate NewCompleted(
        Guid requestId, DateTime completedAt, string appraisalType = AppraisalTypes.ReAppraisal,
        Guid? prevAppraisalId = null, AppraisalStatus? status = null, string? purpose = null)
    {
        var appraisal = AppraisalAggregate.Create(
            requestId, appraisalType, "Normal", DateTime.Now, purpose: purpose, prevAppraisalId: prevAppraisalId);
        appraisal.SetAppraisalNumber($"CRS{Guid.NewGuid():N}"[..10]);
        typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(appraisal, status ?? AppraisalStatus.Completed);
        typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(appraisal, completedAt);
        return appraisal;
    }
}
