using System.Text.Json;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>Wire contract: <c>data</c> is the body of the real PUT for the route's suffix, unchanged.</summary>
public record CorrectPropertyDataRequest(string Reason, JsonElement Data);
