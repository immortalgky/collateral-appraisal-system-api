using System.Data;
using Dapper;
using Shared.Data.Outbox;

namespace Integration.Application.Features.OutboxMessages;

/// <summary>
/// Builds the `UNION ALL` across the six whitelisted `IntegrationEventOutbox` schemas. Every schema
/// name interpolated here comes only from <see cref="OutboxModuleWhitelist.Modules"/> — never from a
/// caller-supplied value — so this is safe despite the string interpolation.
/// </summary>
public static class OutboxUnionSql
{
    /// <summary>
    /// The one definition of "Stuck" (a display-only threshold, see <see cref="OutboxDeliveryPolicy.StuckThreshold"/>):
    /// Processing for longer than <c>@StuckThreshold</c>, or — rolling-deploy safety — a NULL
    /// <c>ProcessingStartedAt</c> on a row whose <c>OccurredAt</c> is also older than it. Shared by the list's
    /// Stuck tab (inside each UNION branch) and the summary's stuck count (over the UNION), so the two can't
    /// drift. Written against the <c>o</c> alias both use; bind the parameter with <see cref="AddStuckThreshold"/>.
    /// </summary>
    public const string StuckPredicate =
        "o.Status = 'Processing' AND (o.ProcessingStartedAt < @StuckThreshold " +
        "OR (o.ProcessingStartedAt IS NULL AND o.OccurredAt < @StuckThreshold))";

    /// <summary>Binds <c>@StuckThreshold</c> as DateTime2, like the columns it is compared with (a plain
    /// DateTime binds as SqlDbType.DateTime, with 3.33 ms rounding).</summary>
    public static void AddStuckThreshold(this DynamicParameters parameters, DateTime now) =>
        parameters.Add("StuckThreshold", now - OutboxDeliveryPolicy.StuckThreshold, DbType.DateTime2);

    /// <param name="selectColumns">Columns pulled from each per-module table.</param>
    /// <param name="whereClause">
    /// An optional predicate — no leading <c>WHERE</c> — applied inside EACH
    /// per-module branch rather than on the UNION's result, so each branch can still seek
    /// <c>IX_IntegrationEventOutbox_Polling</c> (Status, OccurredAt) instead of scanning the whole table
    /// before the filter is applied. The branch's table is aliased <c>o</c>. Interpolated the same way
    /// <paramref name="selectColumns"/> already is: a caller-supplied literal, never end-user input.
    /// </param>
    /// <param name="onlyModule">Emit just this module's branch (must be whitelisted) instead of all six.</param>
    public static string Build(string selectColumns, string? whereClause = null, string? onlyModule = null) =>
        Build(selectColumns, _ => whereClause, onlyModule);

    /// <summary>Like the other overload, but the predicate may depend on the branch's own module.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "module is always one of OutboxModuleWhitelist.Modules' own six hard-coded schema literals — " +
            "never caller input (onlyModule is checked against that whitelist first and only selects from " +
            "it). selectColumns/whereClause are caller-supplied literals built from column names/status " +
            "constants (see call sites), never end-user input; every actual value stays bound as a " +
            "@parameter by the caller's own SQL around this UNION.")]
    public static string Build(string selectColumns, Func<string, string?> whereClauseForModule, string? onlyModule = null)
    {
        if (onlyModule is not null)
            OutboxModuleWhitelist.EnsureValid(onlyModule);

        return string.Join(
            "\nUNION ALL\n",
            OutboxModuleWhitelist.Modules.Where(module => onlyModule is null || module == onlyModule).Select(module =>
            {
                var where = whereClauseForModule(module) is { } clause ? $" WHERE {clause}" : "";
                return $"SELECT N'{module}' AS Module, {selectColumns} FROM [{module}].[IntegrationEventOutbox] o{where}";
            }));
    }
}
