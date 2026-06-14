using FluentValidation;
using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Scopes.CreateScope;

public record CreateScopeCommand(
    string Name,
    string? DisplayName,
    string? Description,
    List<string> Resources
) : ICommand<CreateScopeResult>;

public record CreateScopeResult(string Id, string Name);

public class CreateScopeCommandValidator : AbstractValidator<CreateScopeCommand>
{
    public CreateScopeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(200)
            .Matches("^[a-zA-Z0-9_.:-]+$")
            .WithMessage("Scope name may only contain letters, digits and . _ : - characters.");
    }
}

public class CreateScopeCommandHandler(IOpenIddictScopeManager scopeManager)
    : ICommandHandler<CreateScopeCommand, CreateScopeResult>
{
    public async Task<CreateScopeResult> Handle(CreateScopeCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();

        if (await scopeManager.FindByNameAsync(name, cancellationToken) is not null)
            throw new ConflictException("Scope", name);

        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = name,
            DisplayName = command.DisplayName,
            Description = command.Description
        };
        descriptor.Resources.UnionWith(command.Resources.Where(r => !string.IsNullOrWhiteSpace(r)));

        var created = await scopeManager.CreateAsync(descriptor, cancellationToken);
        var id = await scopeManager.GetIdAsync(created, cancellationToken) ?? "";

        return new CreateScopeResult(id, name);
    }
}
