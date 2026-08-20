using System.Text.Json.Serialization;
using Workflow.Workflow.Config;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Infrastructure.Seed;

/// <summary>
/// Shared logic for the workflow-definition seeders.
/// <para>
/// Two things every seeder must get right, both of which used to be missing:
/// </para>
/// <list type="number">
/// <item>
/// The engine reads its schema from a <b>Published</b> <see cref="WorkflowDefinitionVersion"/>
/// (<c>WorkflowEngine.StartWorkflowAsync</c> refuses to start a workflow without one), so a
/// definition row on its own is useless.
/// </item>
/// <item>
/// The embedded JSON files are in API-envelope shape
/// (<c>{ name, description, category, createdBy, workflowSchema: { ... } }</c>) but the engine
/// deserializes the stored column straight into <see cref="WorkflowSchema"/>. The envelope has to be
/// unwrapped before it is persisted, otherwise the engine sees a schema with zero activities.
/// </item>
/// </list>
/// </summary>
public static class WorkflowDefinitionSeedHelper
{
    /// <summary>
    /// The deserialization options the engine uses in
    /// <c>WorkflowPersistenceService.DeserializeWorkflowSchemaSecurely</c>. Kept identical here so the
    /// seeder validates the JSON exactly the way the runtime will read it.
    /// <para>
    /// Public so read-only consumers (e.g. the task-assignment admin activity picker) parse a stored
    /// schema the same way. The <see cref="JsonStringEnumConverter"/> is load-bearing:
    /// <c>TransitionDefinition.Type</c> is an enum and the stored JSON carries it as a string
    /// (<c>"Conditional"</c>), so deserializing without it throws.
    /// </para>
    /// </summary>
    public static readonly JsonSerializerOptions EngineJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = WorkflowEngineConstants.MaxJsonDeserializationDepth,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Reads an embedded JSON resource, trying each candidate name in turn. Resource names depend on
    /// how the build replaces invalid identifier characters, so callers pass both the hyphen and the
    /// underscore variant.
    /// </summary>
    public static string? LoadEmbeddedResource(Assembly assembly, params string[] candidateNames)
    {
        foreach (var candidate in candidateNames)
        {
            using var stream = assembly.GetManifestResourceStream(candidate);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        return null;
    }

    /// <summary>
    /// Seeds a workflow definition together with a Published v1 version.
    /// Insert-once: does nothing when a definition with the same name already exists, so an
    /// environment whose workflow was edited through the Workflow Builder is never overwritten.
    /// </summary>
    public static async Task SeedAsync(
        WorkflowDbContext context,
        ILogger logger,
        string name,
        string description,
        string category,
        string createdBy,
        string? fileJson)
    {
        if (await context.WorkflowDefinitions.AnyAsync(x => x.Name == name))
        {
            logger.LogInformation("Workflow definition '{Name}' already seeded, skipping", name);
            return;
        }

        if (string.IsNullOrWhiteSpace(fileJson))
        {
            logger.LogWarning("Workflow JSON resource for '{Name}' not found", name);
            return;
        }

        string schemaJson;
        try
        {
            schemaJson = ExtractSchemaJson(fileJson);
            ValidateEngineReadable(schemaJson);
        }
        catch (Exception ex)
        {
            // Persisting a schema the engine cannot parse would leave the workflow permanently
            // unstartable, so refuse to write it and make the reason loud.
            logger.LogError(ex, "Workflow JSON for '{Name}' is not engine-readable; definition not seeded", name);
            return;
        }

        var definition = WorkflowDefinition.Create(
            name: name,
            description: description,
            jsonDefinition: schemaJson,
            category: category,
            createdBy: createdBy);

        // Create() yields a Draft; the engine only ever picks up Published versions.
        var version = WorkflowDefinitionVersion.Create(
            definitionId: definition.Id,
            version: 1,
            name: name,
            description: description,
            jsonSchema: schemaJson,
            category: category,
            createdBy: createdBy);
        version.Publish(createdBy);

        context.WorkflowDefinitions.Add(definition);
        context.WorkflowDefinitionVersions.Add(version);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded workflow definition '{Name}' {DefinitionId} with published version 1 {VersionId}",
            name, definition.Id, version.Id);
    }

    /// <summary>
    /// Unwraps the API envelope so the stored column holds the bare schema the engine expects.
    /// Returns the input unchanged when it is already a bare schema.
    /// </summary>
    public static string ExtractSchemaJson(string fileJson)
    {
        using var doc = JsonDocument.Parse(fileJson);
        return doc.RootElement.TryGetProperty("workflowSchema", out var schema)
            ? schema.GetRawText()
            : fileJson;
    }

    /// <summary>
    /// Proves the JSON about to be stored can be turned back into a usable <see cref="WorkflowSchema"/>
    /// by the runtime. Throws when it cannot.
    /// </summary>
    private static void ValidateEngineReadable(string schemaJson)
    {
        if (schemaJson.Length > WorkflowEngineConstants.MaxWorkflowDefinitionJsonSize)
            throw new InvalidOperationException("Workflow schema JSON exceeds the engine's maximum size limit");

        var schema = JsonSerializer.Deserialize<WorkflowSchema>(schemaJson, EngineJsonOptions)
                     ?? throw new InvalidOperationException("Workflow schema JSON deserialized to null");

        if (string.IsNullOrWhiteSpace(schema.Name))
            throw new InvalidOperationException("Workflow schema must have a valid name");

        if (schema.Activities.Count == 0)
            throw new InvalidOperationException(
                "Workflow schema must have at least one activity (is the JSON still wrapped in a 'workflowSchema' envelope?)");

        if (schema.Activities.Any(a => string.IsNullOrWhiteSpace(a.Id)))
            throw new InvalidOperationException("All activities must have valid IDs");
    }
}
