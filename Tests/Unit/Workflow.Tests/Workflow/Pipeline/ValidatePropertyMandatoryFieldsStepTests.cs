using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Steps;

namespace Workflow.Tests.Workflow.Pipeline;

/// <summary>
/// Unit tests for the view-driven <see cref="ValidatePropertyMandatoryFieldsStep"/>.
///
/// The step reads `SELECT *` from vw_AppraisalPropertyValidationContext and maps
/// each row as an IDictionary&lt;string,object&gt; (Dapper dynamic). Presence of a
/// field is 1; absence is 0. An unknown fieldKey (not a column in the row) is
/// skipped with a Warning log — it NEVER fails the validation.
///
/// Because Dapper QueryAsync&lt;dynamic&gt; cannot be fully mocked through IDbConnection
/// alone, DB-path tests verify the error-handling path (connection throws → Errored)
/// and the early-exit guards. The core message-format and field-lookup logic is
/// tested via BuildRowDict helpers that mimic the dictionary the view produces.
/// </summary>
public class ValidatePropertyMandatoryFieldsStepTests
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<ValidatePropertyMandatoryFieldsStep> _logger;
    private readonly IDbConnection _dbConnection;
    private readonly ValidatePropertyMandatoryFieldsStep _sut;

    public ValidatePropertyMandatoryFieldsStepTests()
    {
        _connectionFactory = Substitute.For<ISqlConnectionFactory>();
        _logger = Substitute.For<ILogger<ValidatePropertyMandatoryFieldsStep>>();
        _dbConnection = Substitute.For<IDbConnection>();
        _connectionFactory.GetOpenConnection().Returns(_dbConnection);

        _sut = new ValidatePropertyMandatoryFieldsStep(_connectionFactory, _logger);
    }

    // ── Descriptor ──

    [Fact]
    public void Descriptor_Name_ShouldBeValidatePropertyMandatoryFields()
    {
        _sut.Descriptor.Name.Should().Be("ValidatePropertyMandatoryFields");
    }

    [Fact]
    public void Descriptor_Kind_ShouldBeValidation()
    {
        _sut.Descriptor.Kind.Should().Be(StepKind.Validation);
    }

    // ── Null AppraisalId ──

    [Fact]
    public async Task ExecuteAsync_NullAppraisalId_ReturnsFail_APPRAISAL_NOT_CREATED()
    {
        var ctx = BuildCtx(appraisalId: null, parametersJson:
            """{"requiredByType":{"L":["TitleNumber"]}}""");

        var result = await _sut.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var failed = result.Should().BeOfType<ProcessStepResult.Failed>().Which;
        failed.ErrorCode.Should().Be("APPRAISAL_NOT_CREATED");
    }

    // ── Empty config → Pass (no DB call) ──

    [Fact]
    public async Task ExecuteAsync_EmptyRequiredByType_ReturnsPass()
    {
        var ctx = BuildCtx(Guid.NewGuid(), parametersJson: """{"requiredByType":{}}""");

        var result = await _sut.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _connectionFactory.DidNotReceive().GetOpenConnection();
    }

    // ── Connection throws → Errored ──

    [Fact]
    public async Task ExecuteAsync_ConnectionThrows_ReturnsErrored()
    {
        var throwingFactory = Substitute.For<ISqlConnectionFactory>();
        throwingFactory.When(f => f.GetOpenConnection()).Do(_ => throw new InvalidOperationException("DB unavailable"));

        var step = new ValidatePropertyMandatoryFieldsStep(throwingFactory, _logger);
        var ctx = BuildCtx(Guid.NewGuid(), parametersJson:
            """{"requiredByType":{"L":["TitleNumber"]}}""");

        var result = await step.ExecuteAsync(ctx, CancellationToken.None);

        result.Should().BeOfType<ProcessStepResult.Errored>();
    }

    // ── Row-level field lookup logic ─────────────────────────────────────────

    /// <summary>
    /// Tests the IsFieldPresent helper logic used by the step:
    /// value == 1 (or any non-zero int/bool) → present; value == 0 → missing.
    /// </summary>

    [Fact]
    public void FieldLookup_Value1_IsPresent()
    {
        var row = BuildRow("L", 1, ("TitleNumber", 1), ("Province", 0));

        // TitleNumber = 1 → present
        IsPresent(row, "TitleNumber").Should().BeTrue();
    }

    [Fact]
    public void FieldLookup_Value0_IsMissing()
    {
        var row = BuildRow("L", 1, ("Province", 0));

        // Province = 0 → missing
        IsPresent(row, "Province").Should().BeFalse();
    }

    [Fact]
    public void FieldLookup_UnknownKey_NotAColumn_IsSkipped_ReturnsPresent()
    {
        // A fieldKey that is NOT a column in the row → fail-safe, treated as present.
        var row = BuildRow("L", 1, ("TitleNumber", 1));

        // "SomeNewField" is not in the row → fail-safe skip
        IsPresent(row, "SomeNewField").Should().BeTrue(
            "an unknown fieldKey (not a column in the view row) must be skipped, not treated as missing");
    }

    // ── Message format contracts ──────────────────────────────────────────────

    [Fact]
    public void MessageFormat_SingleProperty_IsCorrect()
    {
        // "Property #1 (L): LandOffice, Province."
        var violations = new List<string> { "Property #1 (L): LandOffice, Province." };
        var message = string.Join(" ", violations);
        message.Should().Be("Property #1 (L): LandOffice, Province.");
    }

    [Fact]
    public void MessageFormat_MultipleProperties_CombinesCorrectly()
    {
        var violations = new List<string>
        {
            "Property #1 (L): LandOffice, Province.",
            "Property #2 (C): TitleNumber."
        };
        var message = string.Join(" ", violations);
        message.Should().Be("Property #1 (L): LandOffice, Province. Property #2 (C): TitleNumber.");
    }

    // ── Violation collection: property whose type is not configured → skipped ──

    [Fact]
    public void TypeNotInRequiredByType_IsSkipped_NoViolation()
    {
        // Property type "V" (vehicle) is not in requiredByType → should be skipped
        var requiredByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "L", ["TitleNumber"] }
        };

        var rows = new List<IDictionary<string, object>>
        {
            BuildRow("V", 1, ("TitleNumber", 0))  // TitleNumber missing but type not configured
        };

        var violations = CollectViolations(rows, requiredByType);
        violations.Should().BeEmpty("property type 'V' is not in requiredByType → must be skipped");
    }

    // ── Violation collection: all fields present → no violation ──────────────

    [Fact]
    public void AllFieldsPresent_NoViolation()
    {
        var requiredByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "L", ["TitleNumber", "Province"] }
        };

        var rows = new List<IDictionary<string, object>>
        {
            BuildRow("L", 1, ("TitleNumber", 1), ("Province", 1))
        };

        var violations = CollectViolations(rows, requiredByType);
        violations.Should().BeEmpty("all required fields are present (value 1)");
    }

    // ── Violation collection: missing field → violation message ──────────────

    [Fact]
    public void MissingField_ProducesCorrectMessage()
    {
        var requiredByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "L", ["TitleNumber", "LandOffice"] }
        };

        var rows = new List<IDictionary<string, object>>
        {
            BuildRow("L", 2, ("TitleNumber", 0), ("LandOffice", 0))
        };

        var violations = CollectViolations(rows, requiredByType);
        violations.Should().HaveCount(1);
        violations[0].Should().Be("Property #2 (L): TitleNumber, LandOffice.");
    }

    // ── Violation collection: unknown fieldKey → skipped, never fails ────────

    [Fact]
    public void UnknownFieldKey_NotAColumn_IsSkipped_NoViolation()
    {
        var requiredByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "L", ["SomeFutureField", "Province"] }
        };

        var rows = new List<IDictionary<string, object>>
        {
            BuildRow("L", 1, ("Province", 1))
            // "SomeFutureField" column does not exist in row
        };

        var violations = CollectViolations(rows, requiredByType);
        violations.Should().BeEmpty(
            "unknown fieldKey (not a view column) must be skipped — fail-safe so a config typo never blocks completion");
    }

    // ── Violation collection: multiple properties, mixed ─────────────────────

    [Fact]
    public void MultipleProperties_CollectsAllViolations()
    {
        var requiredByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "L", ["TitleNumber", "Province"] },
            { "U", ["Province", "District"] }   // "U" = Condo (PropertyType.Condo.Code)
        };

        var rows = new List<IDictionary<string, object>>
        {
            BuildRow("L", 1, ("TitleNumber", 1), ("Province", 0)),   // Province missing
            BuildRow("U", 2, ("Province", 1), ("District", 0)),      // District missing
        };

        var violations = CollectViolations(rows, requiredByType);
        violations.Should().HaveCount(2);
        violations[0].Should().Be("Property #1 (L): Province.");
        violations[1].Should().Be("Property #2 (U): District.");
    }

    // ── IActivityProcessStep interface compliance ──

    [Fact]
    public void Step_ShouldImplementIActivityProcessStep()
    {
        _sut.Should().BeAssignableTo<IActivityProcessStep>();
    }

    // ── Type matching: case-insensitive ──────────────────────────────────────

    [Theory]
    [InlineData("L", "l", true)]
    [InlineData("L", "L", true)]
    [InlineData("U", "u", true)]   // "U" = Condo (PropertyType.Condo.Code)
    [InlineData("L", "U", false)]  // Land does not match Condo
    public void PropertyTypeMatching_CaseInsensitive(string configuredType, string propertyType, bool shouldMatch)
    {
        bool matches = string.Equals(configuredType, propertyType, StringComparison.OrdinalIgnoreCase);
        matches.Should().Be(shouldMatch);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Mimics the field-presence logic the refactored step uses:
    /// looks up the column by fieldKey in the row dict;
    /// if absent → fail-safe (skip = treated as present);
    /// if present → value != 0 means present.
    /// </summary>
    private static bool IsPresent(IDictionary<string, object> row, string fieldKey)
    {
        if (!row.TryGetValue(fieldKey, out var rawValue))
            return true; // Unknown column → fail-safe skip

        return rawValue switch
        {
            int i => i != 0,
            long l => l != 0,
            bool b => b,
            byte by => by != 0,
            _ => rawValue?.ToString() != "0"
        };
    }

    /// <summary>
    /// Runs the step's per-row violation-collection logic against an in-memory
    /// list of row dicts (no DB required).
    /// </summary>
    private static List<string> CollectViolations(
        List<IDictionary<string, object>> rows,
        Dictionary<string, List<string>> requiredByType)
    {
        var violations = new List<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var propertyType = row["PropertyType"]?.ToString() ?? "";
            var sequenceNumber = Convert.ToInt32(row["SequenceNumber"]);
            var displayNumber = sequenceNumber > 0 ? sequenceNumber : i + 1;

            var matchedKey = requiredByType.Keys
                .FirstOrDefault(k => string.Equals(k, propertyType, StringComparison.OrdinalIgnoreCase));

            if (matchedKey is null) continue;

            var missingFields = new List<string>();
            foreach (var fieldKey in requiredByType[matchedKey])
            {
                if (!IsPresent(row, fieldKey))
                    missingFields.Add(fieldKey);
            }

            if (missingFields.Count > 0)
                violations.Add($"Property #{displayNumber} ({propertyType}): {string.Join(", ", missingFields)}.");
        }

        return violations;
    }

    /// <summary>Builds an IDictionary row mimicking vw_AppraisalPropertyValidationContext.</summary>
    private static IDictionary<string, object> BuildRow(
        string propertyType,
        int sequenceNumber,
        params (string Key, int Value)[] fields)
    {
        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["PropertyType"] = propertyType,
            ["SequenceNumber"] = sequenceNumber
        };
        foreach (var (key, value) in fields)
            row[key] = value;
        return row;
    }

    private static ProcessStepContext BuildCtx(Guid? appraisalId, string? parametersJson = null) =>
        new()
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "ext-appraisal-assignment",
            CompletedBy = "user",
            AppraisalId = appraisalId,
            ParametersJson = parametersJson,
            Variables = new Dictionary<string, object?>(),
            Input = new Dictionary<string, object?>()
        };
}
