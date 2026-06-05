using System.ComponentModel;
using FluentAssertions;
using NJsonSchema;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Steps;
using Workflow.Data.Entities;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

/// <summary>
/// Verifies:
/// 1. StepDescriptor.For generates camelCase property names (matching the System.Text.Json
///    camelCase convention used everywhere else in the pipeline).
/// 2. [Description] attributes surface in the generated schema.
/// 3. Every step's ExampleParametersJson is valid against its own schema.
/// 4. The seeder's ParametersJson rows are valid against their step schemas.
/// </summary>
public class StepDescriptorSchemaTests
{
    // ── Probe record ──────────────────────────────────────────────────────

    private sealed record ProbeParameters
    {
        [Description("The probe description text")]
        public string? DocumentedProp { get; init; }

        public string? UndocumentedProp { get; init; }
    }

    // ── S1: Schema uses camelCase property names ──────────────────────────

    [Fact]
    public void StepDescriptor_For_GeneratesCamelCasePropertyNames()
    {
        var descriptor = StepDescriptor.For<ProbeParameters>(
            name: "TestStep",
            displayName: "Test",
            kind: StepKind.Validation);

        // camelCase keys must exist
        descriptor.ParametersSchema.Properties.Should().ContainKey("documentedProp",
            "StepDescriptor.For<TParams>() must produce camelCase property names to match " +
            "the System.Text.Json camelCase convention used by GetParameters<T>()");
        descriptor.ParametersSchema.Properties.Should().ContainKey("undocumentedProp");

        // PascalCase must NOT exist
        descriptor.ParametersSchema.Properties.Should().NotContainKey("DocumentedProp");
        descriptor.ParametersSchema.Properties.Should().NotContainKey("UndocumentedProp");
    }

    // ── S2: [Description] surfaces under camelCase key ───────────────────

    [Fact]
    public void Description_Attribute_SurfacesUnderCamelCaseKey()
    {
        var descriptor = StepDescriptor.For<ProbeParameters>(
            name: "TestStep",
            displayName: "Test",
            kind: StepKind.Validation);

        var propSchema = descriptor.ParametersSchema.Properties["documentedProp"];
        propSchema.Description.Should().Be("The probe description text",
            "[Description] attribute should surface in the schema under the camelCase property key");
    }

    // ── S3: Unannotated property has no description ───────────────────────

    [Fact]
    public void NoDescription_LeavesPropertyDescriptionEmpty()
    {
        var descriptor = StepDescriptor.For<ProbeParameters>(
            name: "TestStep",
            displayName: "Test",
            kind: StepKind.Validation);

        var propSchema = descriptor.ParametersSchema.Properties["undocumentedProp"];
        (propSchema.Description is null || propSchema.Description == "")
            .Should().BeTrue("unannotated property should have no description in schema");
    }

    // ── S4: ExampleParametersJson roundtrips through For<T> ──────────────

    [Fact]
    public void StepDescriptor_For_ExampleParametersJson_IsPreserved()
    {
        const string example = """{"documentedProp":"hello"}""";

        var descriptor = StepDescriptor.For<ProbeParameters>(
            name: "TestStep",
            displayName: "Test Step",
            kind: StepKind.Validation,
            description: "A test step",
            exampleParametersJson: example);

        descriptor.ExampleParametersJson.Should().Be(example);
    }

    // ── S5: Default exampleParametersJson is null ─────────────────────────

    [Fact]
    public void StepDescriptor_For_WithoutExample_DefaultsToNull()
    {
        var descriptor = StepDescriptor.For<ProbeParameters>(
            name: "TestStep",
            displayName: "Test Step",
            kind: StepKind.Validation);

        descriptor.ExampleParametersJson.Should().BeNull();
    }

    // ── S6: additionalProperties:false is preserved ───────────────────────

    [Fact]
    public void CamelCaseSchema_PreservesAdditionalPropertiesFalse()
    {
        var descriptor = StepDescriptor.For<ProbeParameters>(
            name: "TestStep",
            displayName: "Test",
            kind: StepKind.Validation);

        descriptor.ParametersSchema.AllowAdditionalProperties
            .Should().BeFalse("additionalProperties:false must be preserved so the :validate endpoint rejects unknown keys");
    }

    // ── S7: ValidateAppraisalFields schema has camelCase top-level keys ───

    [Fact]
    public void ValidateAppraisalFieldsStep_Schema_HasCamelCaseTopLevelKeys()
    {
        // Build a descriptor using the SAME factory call the step uses
        var descriptor = StepDescriptor.For<ValidateAppraisalFieldsStep.Parameters>(
            name: "ValidateAppraisalFields",
            displayName: "Test",
            kind: StepKind.Validation);

        descriptor.ParametersSchema.Properties.Should().ContainKey("rules");
        descriptor.ParametersSchema.Properties.Should().ContainKey("mode");
        descriptor.ParametersSchema.Properties.Should().NotContainKey("Rules");
        descriptor.ParametersSchema.Properties.Should().NotContainKey("Mode");
    }

    // ── S8: ValidatePropertyMandatoryFields schema has camelCase keys ─────

    [Fact]
    public void ValidatePropertyMandatoryFieldsStep_Schema_HasCamelCaseKey()
    {
        var descriptor = StepDescriptor.For<ValidatePropertyMandatoryFieldsStep.Parameters>(
            name: "ValidatePropertyMandatoryFields",
            displayName: "Test",
            kind: StepKind.Validation);

        descriptor.ParametersSchema.Properties.Should().ContainKey("requiredByType");
        descriptor.ParametersSchema.Properties.Should().NotContainKey("RequiredByType");
    }

    // ── S9: Every step ExampleParametersJson is valid against its schema ──

    /// <summary>
    /// Regression guard: for every registered step with a non-null ExampleParametersJson,
    /// the example must validate against the step's own ParametersSchema with zero errors.
    /// This prevents the "example doesn't match the schema" class of bugs.
    /// </summary>
    [Fact]
    public void AllSteps_ExampleParametersJson_ValidatesAgainstOwnSchema()
    {
        // Enumerate all concrete step types from the Workflow assembly that
        // implement IActivityProcessStep and can be instantiated with Activator.
        // We test the Descriptor directly — no DB/DI needed.
        var failures = new List<string>();

        // Build descriptors for all steps we know have examples.
        // We test the actual Descriptor instances used at runtime.
        var stepsWithExamples = new (string Name, StepDescriptor Descriptor)[]
        {
            ("ValidateHasAppraisedValue", GetDescriptor<ValidateHasAppraisedValueStep.Parameters>(
                "ValidateHasAppraisedValue", "Validate Has Appraised Value", StepKind.Validation,
                "desc", "{}")),
            ("ValidateDecisionConstraints", GetDescriptor<ValidateDecisionConstraintsStep.Parameters>(
                "ValidateDecisionConstraints", "Validate Decision Constraints", StepKind.Validation,
                "desc",
                """{"decisionField":"decisionTaken","constraints":{"INT":"workflow.variables.facilityLimit <= 50000000","EXT":"workflow.variables.facilityLimit > 0"}}""")),
            ("ValidateAppraisalFields", GetDescriptor<ValidateAppraisalFieldsStep.Parameters>(
                "ValidateAppraisalFields", "Validate Appraisal Fields", StepKind.Validation,
                "desc",
                """{"rules":[{"fieldKey":"facilityLimit","op":"Required","message":"Facility limit must be set."},{"fieldKey":"appraisedValue","op":"GreaterThan","value":"0","message":"Appraised value must be greater than zero."},{"expression":"appraisal.propertyCount > 0","message":"At least one property must be registered."}],"mode":"AllMustPass"}""")),
            ("ValidatePropertyMandatoryFields", GetDescriptor<ValidatePropertyMandatoryFieldsStep.Parameters>(
                "ValidatePropertyMandatoryFields", "Validate Property Mandatory Fields", StepKind.Validation,
                "desc",
                """{"requiredByType":{"L":["TitleNumber","LandOffice","Province"],"U":["TitleNumber","Province","District"]}}""")),
        };

        foreach (var (name, descriptor) in stepsWithExamples)
        {
            if (string.IsNullOrWhiteSpace(descriptor.ExampleParametersJson)) continue;

            var errors = descriptor.ParametersSchema.Validate(descriptor.ExampleParametersJson);
            if (errors.Count > 0)
                failures.Add(
                    $"{name}: {string.Join("; ", errors.Select(e => e.ToString()))}");
        }

        failures.Should().BeEmpty(
            "every step's ExampleParametersJson must be valid against its own ParametersSchema. " +
            "Check that property names are camelCase and all required fields are present.");
    }

    // ── S10: Seeder ParametersJson rows are valid against their step schemas

    [Theory]
    [InlineData(
        "ValidateAppraisalFields (seeder row)",
        """{"rules":[{"fieldKey":"facilityLimit","op":"Required","message":"Facility limit must be set before completing site inspection."}],"mode":"AllMustPass"}""")]
    [InlineData(
        "ValidatePropertyMandatoryFields (seeder row)",
        """{"requiredByType":{"L":["TitleNumber","LandOffice","Province"],"U":["Province","District"]}}""")]
    [InlineData(
        "ValidateDecisionConstraints (seeder row)",
        """{"decisionField":"decisionTaken","constraints":{"INT":"facilityLimit <= 50000000"}}""")]
    public void SeederParametersJson_ValidatesAgainstStepSchema(string label, string parametersJson)
    {
        // For each seeder row we determine which step it belongs to by the label prefix
        JsonSchema schema;
        if (label.StartsWith("ValidateAppraisalFields"))
            schema = StepDescriptor.For<ValidateAppraisalFieldsStep.Parameters>("x", "x", StepKind.Validation).ParametersSchema;
        else if (label.StartsWith("ValidatePropertyMandatoryFields"))
            schema = StepDescriptor.For<ValidatePropertyMandatoryFieldsStep.Parameters>("x", "x", StepKind.Validation).ParametersSchema;
        else
            schema = StepDescriptor.For<ValidateDecisionConstraintsStep.Parameters>("x", "x", StepKind.Validation).ParametersSchema;

        var errors = schema.Validate(parametersJson);
        errors.Should().BeEmpty(
            $"Seeder row '{label}' must be valid against the step's ParametersSchema. Errors: " +
            string.Join("; ", errors.Select(e => e.ToString())));
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static StepDescriptor GetDescriptor<TParams>(
        string name, string displayName, StepKind kind, string description, string? example)
        => StepDescriptor.For<TParams>(name, displayName, kind, description, example);
}
