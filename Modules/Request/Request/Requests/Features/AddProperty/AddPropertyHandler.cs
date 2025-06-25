namespace Request.Requests.Features.AddProperty;

public record AddPropertyCommand (long Id, List<PropertyDto> Property) : ICommand<AddPropertyResult>;
public record AddPropertyResult (bool IsSuccess);

internal class AddPropertyHandler(RequestDbContext dbContext)
    : ICommandHandler<AddPropertyCommand, AddPropertyResult>
{
    public async Task<AddPropertyResult> Handle(AddPropertyCommand command, CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([command.Id], cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);
        foreach (var property in command.Property)
        {
            request.AddProperty(CreateNewProperty(property));
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddPropertyResult(true);
    }

    private static RequestProperty CreateNewProperty(PropertyDto property)
    {
        return RequestProperty.Of(property.PropertyType, property.BuildingType, property.SellingPrice);
    }
}