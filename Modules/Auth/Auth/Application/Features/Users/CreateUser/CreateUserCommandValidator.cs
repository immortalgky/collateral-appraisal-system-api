using Auth.Application.Configurations;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Auth.Application.Features.Users.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator(IOptions<LdapConfiguration> ldapOptions)
    {
        var ldapEnabled = ldapOptions.Value.Enabled;

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(256);
        RuleFor(x => x.AuthSource)
            .Must(s => s is AuthSources.Local or AuthSources.Ldap)
            .WithMessage("AuthSource must be 'Local' or 'LDAP'.");
        // Block creating an LDAP-authenticated user while LDAP is off — it could never log in
        // (no local password, and the LDAP path is gated on Ldap:Enabled).
        RuleFor(x => x.AuthSource)
            .Must(_ => ldapEnabled)
            .When(x => AuthSources.IsLdap(x.AuthSource))
            .WithMessage("Cannot create an LDAP user while LDAP authentication is disabled.");
        // LDAP users authenticate against AD, so a local password is not required for them.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .When(x => !AuthSources.IsLdap(x.AuthSource));
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.");
        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Position cannot exceed 100 characters.")
            .When(x => x.Position != null);
        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("Department cannot exceed 100 characters.")
            .When(x => x.Department != null);
        RuleFor(x => x.AoCode)
            .MaximumLength(10).WithMessage("AO Code cannot exceed 10 characters.")
            .When(x => x.AoCode != null);
        RuleFor(x => x.Roles).NotNull().WithMessage("Roles are required.");
        RuleForEach(x => x.Roles).NotEmpty().WithMessage("RoleId is required.");
    }
}
