namespace Appraisal.Application.Features.Project.CorrectProjectData;

public record CorrectProjectDataResult(int ChangedFieldCount, IReadOnlyList<string> ChangedFields);