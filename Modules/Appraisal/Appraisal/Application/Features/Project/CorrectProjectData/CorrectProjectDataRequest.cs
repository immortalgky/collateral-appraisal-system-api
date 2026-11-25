using System.Text.Json;

namespace Appraisal.Application.Features.Project.CorrectProjectData;

public record CorrectProjectDataRequest(string Reason, JsonElement Data);