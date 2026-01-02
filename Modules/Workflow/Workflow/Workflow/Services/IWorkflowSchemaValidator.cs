using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Services;

/// <summary>
/// Service responsible for validating and deserializing workflow schemas
/// Extracted from WorkflowService to separate infrastructure concerns
/// </summary>
public interface IWorkflowSchemaValidator
{
    /// <summary>
    /// Deserializes a workflow schema from JSON with security validations
    /// </summary>
    /// <param name="jsonDefinition">JSON definition of the workflow schema</param>
    /// <param name="workflowDefinitionId">Workflow definition ID for error context</param>
    /// <returns>Deserialized workflow schema or null if invalid</returns>
    WorkflowSchema? DeserializeWorkflowSchemaSecurely(string jsonDefinition, Guid workflowDefinitionId);

    /// <summary>
    /// Validates the structure and content of a workflow schema
    /// </summary>
    /// <param name="workflowSchema">The workflow schema to validate</param>
    void ValidateWorkflowSchemaStructure(WorkflowSchema workflowSchema);
}