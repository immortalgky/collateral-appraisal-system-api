using Auth.Application.Configurations;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Auth.Application.Features.Users.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator(IOptions<LdapConfiguration> ldapOptions)
    {
        var ldapEnabled = ldapOptions.Value.Enabled;

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Position).MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.AoCode).MaximumLength(10);

        // AuthSource is optional on update (null = unchanged). Validate only when a value is sent.
        When(x => x.AuthSource is not null, () =>
        {
            RuleFor(x => x.AuthSource)
                .Must(v => v is AuthSources.Local or AuthSources.Ldap)
                .WithMessage("AuthSource must be 'Local' or 'LDAP'.");
            // Mirror CreateUser: don't let an account be switched to LDAP while LDAP is disabled —
            // it could never authenticate.
            RuleFor(x => x.AuthSource)
                .Must(_ => ldapEnabled)
                .When(x => AuthSources.IsLdap(x.AuthSource))
                .WithMessage("Cannot set AuthSource to LDAP while LDAP authentication is disabled.");
        });
    }
}
