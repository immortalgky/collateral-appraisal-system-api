using System.Text.Json;
using FluentAssertions;
using Workflow.Workflow.Infrastructure.Seed;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Services.Configuration;

/// <summary>
/// Pins the deserialization contract the task-assignment activity picker
/// (<c>TaskAssignmentConfigAdminEndpoints.ListActivities</c>) depends on.
///
/// <para>
/// The picker used to parse the stored schema with bare
/// <c>new JsonSerializerOptions { PropertyNameCaseInsensitive = true }</c>. That throws on the real
/// appraisal schema because <see cref="TransitionDefinition.Type"/> is an enum stored as the string
/// <c>"Conditional"</c>, and the endpoint swallowed the exception into a 500 — which the admin page
/// rendered as "Activity list unavailable".
/// </para>
/// </summary>
public class ActivityPickerSchemaParsingTests
{
    private static string LoadAppraisalSchemaJson()
    {
        var fileJson = WorkflowDefinitionSeedHelper.LoadEmbeddedResource(
            typeof(AppraisalWorkflowDefinitionSeeder).Assembly,
            "Workflow.Workflow.Config.appraisal-workflow.json");

        fileJson.Should().NotBeNull("the appraisal workflow JSON must be embedded in the Workflow assembly");

        // Stored column holds the unwrapped schema, not the API envelope.
        return WorkflowDefinitionSeedHelper.ExtractSchemaJson(fileJson!);
    }

    [Fact]
    public void BareCaseInsensitiveOptions_Throw_OnStringBackedTransitionTypeEnum()
    {
        var schemaJson = LoadAppraisalSchemaJson();

        var act = () => JsonSerializer.Deserialize<WorkflowSchema>(
            schemaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Documents exactly why the picker must not use these options.
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void EngineJsonOptions_ParseSchema_AndYieldTaskActivities()
    {
        var schemaJson = LoadAppraisalSchemaJson();

        var schema = JsonSerializer.Deserialize<WorkflowSchema>(
            schemaJson,
            WorkflowDefinitionSeedHelper.EngineJsonOptions);

        schema.Should().NotBeNull();

        var pickerOptions = schema!.Activities
            .Where(a => a.Type is ActivityTypes.TaskActivity or ActivityTypes.FanOutTaskActivity)
            .Select(a => a.Id)
            .ToList();

        pickerOptions.Should().NotBeEmpty("the picker is empty otherwise and the admin page shows 'unavailable'");
        pickerOptions.Should().Contain("int-appraisal-check");
        pickerOptions.Should().Contain("int-appraisal-verification");
    }
}
