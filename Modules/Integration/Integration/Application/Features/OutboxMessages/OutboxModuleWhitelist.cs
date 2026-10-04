using FluentValidation;
using FluentValidation.Results;

namespace Integration.Application.Features.OutboxMessages;

/// <summary>
/// The six `IntegrationEventOutbox` schemas (docs/failed-messages/api-contract.md "Outbox module
/// whitelist"). The module value IS the schema name (lowercase) — checked here before it is ever
/// interpolated into a schema-qualified query, so an arbitrary/injection-like value never reaches SQL.
/// </summary>
public static class OutboxModuleWhitelist
{
    public static readonly string[] Modules =
        ["request", "appraisal", "document", "workflow", "collateral", "reporting"];

    public static bool IsValid(string? module) => module is not null && Array.IndexOf(Modules, module) >= 0;

    /// <summary>
    /// Defence in depth: the same check the FluentValidation validators already run before
    /// MediatR calls the handler, re-run INSIDE the handler itself before any SQL — so a caller that
    /// somehow reaches the handler without going through validation (a future refactor, a direct call)
    /// still can't get an arbitrary string interpolated into a schema-qualified query. Throws the same
    /// exception shape the validator would, so the 400 response is identical either way.
    /// </summary>
    public static void EnsureValid(string? module)
    {
        if (IsValid(module))
            return;

        throw new ValidationException(
        [
            new ValidationFailure(nameof(module), $"Module must be one of: {string.Join(", ", Modules)}.")
        ]);
    }
}
