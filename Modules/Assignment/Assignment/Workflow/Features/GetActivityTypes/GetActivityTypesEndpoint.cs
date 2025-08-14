using Assignment.Workflow.Activities.Factories;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Features.GetActivityTypes;

public class GetActivityTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/activity-types", async (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetActivityTypesQuery();
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetActivityTypes")
            .WithTags("Workflows");

        app.MapGet("/api/workflows/activity-types/{activityType}", async (
                string activityType,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetActivityTypeDefinitionQuery { ActivityType = activityType };
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetActivityTypeDefinition")
            .WithTags("Workflows");
    }
}

public record GetActivityTypesQuery : IRequest<GetActivityTypesResponse>
{
}

public record GetActivityTypesResponse
{
    public List<ActivityTypeDto> ActivityTypes { get; init; } = new();
}

public record GetActivityTypeDefinitionQuery : IRequest<ActivityTypeDefinition>
{
    public string ActivityType { get; init; } = default!;
}

public record ActivityTypeDto
{
    public string Type { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Icon { get; init; } = default!;
    public string Color { get; init; } = default!;
}

public class GetActivityTypesQueryHandler : IRequestHandler<GetActivityTypesQuery, GetActivityTypesResponse>
{
    private readonly IWorkflowActivityFactory _activityFactory;

    public GetActivityTypesQueryHandler(IWorkflowActivityFactory activityFactory)
    {
        _activityFactory = activityFactory;
    }

    public async Task<GetActivityTypesResponse> Handle(GetActivityTypesQuery request,
        CancellationToken cancellationToken)
    {
        var activityTypes = _activityFactory.GetAvailableActivityTypes();

        var dtos = activityTypes.Select(type =>
        {
            var definition = _activityFactory.GetActivityTypeDefinition(type);
            return new ActivityTypeDto
            {
                Type = definition.Type,
                Name = definition.Name,
                Description = definition.Description,
                Category = definition.Category,
                Icon = definition.Icon,
                Color = definition.Color
            };
        }).ToList();

        return new GetActivityTypesResponse { ActivityTypes = dtos };
    }
}

public class
    GetActivityTypeDefinitionQueryHandler : IRequestHandler<GetActivityTypeDefinitionQuery, ActivityTypeDefinition>
{
    private readonly IWorkflowActivityFactory _activityFactory;

    public GetActivityTypeDefinitionQueryHandler(IWorkflowActivityFactory activityFactory)
    {
        _activityFactory = activityFactory;
    }

    public async Task<ActivityTypeDefinition> Handle(GetActivityTypeDefinitionQuery request,
        CancellationToken cancellationToken)
    {
        return _activityFactory.GetActivityTypeDefinition(request.ActivityType);
    }
}