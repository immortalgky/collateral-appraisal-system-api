using System.Text.Json;

namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// Owned method detail for an IncomeAssumption.
/// <para>
/// <see cref="Detail"/> carries the raw method parameters as a passthrough
/// <see cref="JsonElement"/> so the frontend receives the exact same JSON shape it stored,
/// without any server-side round-tripping through strongly-typed records.
/// </para>
/// </summary>
public record IncomeMethodDto(
    string MethodTypeCode,
    JsonElement Detail,
    decimal[] TotalMethodValues
);
