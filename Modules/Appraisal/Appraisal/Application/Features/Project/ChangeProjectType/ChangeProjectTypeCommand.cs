using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.ChangeProjectType;

/// <summary>
/// Command to atomically destroy the existing Project for an appraisal and recreate it
/// with a different ProjectType, preserving all shared (non-type-specific) fields.
/// Manages its own two-phase transaction (delete then insert) and therefore does NOT
/// implement <see cref="ITransactionalCommand{T}"/>.
/// </summary>
public record ChangeProjectTypeCommand(
    Guid AppraisalId,
    ProjectType NewProjectType
) : ICommand<ChangeProjectTypeResult>;
