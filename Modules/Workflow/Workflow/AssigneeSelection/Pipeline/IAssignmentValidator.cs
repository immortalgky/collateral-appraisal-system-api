namespace Workflow.AssigneeSelection.Pipeline;

public interface IAssignmentValidator
{
    Task<AssignmentValidationResult> ValidateAsync(AssignmentPipelineContext context, CancellationToken cancellationToken = default);
}

public record AssignmentValidationResult(bool IsValid, List<string> Errors)
{
    public static AssignmentValidationResult Valid() => new(true, []);
    public static AssignmentValidationResult Invalid(params string[] errors) => new(false, [..errors]);
}
