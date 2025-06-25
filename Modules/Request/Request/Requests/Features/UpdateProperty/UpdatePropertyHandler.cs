namespace Request.Requests.Features.UpdataProperty;

public record UpdatePropertyCommand (long Id, List<PropertyDto> Property) : ICommand<UpdatePropertyResult>;
public record UpdatePropertyResult (bool IsSuccess);

internal class UpdatePropertyHandler(RequestDbContext dbContext)
    : ICommandHandler<UpdatePropertyCommand, UpdatePropertyResult>
{
    public async Task<UpdatePropertyResult> Handle(UpdatePropertyCommand command, CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([command.Id], cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);
        request.ClearProperty();

        foreach (var property in command.Property)
        {
            request.AddProperty(CreateNewProperty(property));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return new UpdatePropertyResult(true);
    }

    private static RequestProperty CreateNewProperty(PropertyDto property)
    {
        return RequestProperty.Of(property.PropertyType, property.BuildingType, property.SellingPrice);
    }
}