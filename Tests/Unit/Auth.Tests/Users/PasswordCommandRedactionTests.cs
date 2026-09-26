using Auth.Application.Features.Users.ChangePassword;
using Auth.Application.Features.Users.CreateUser;
using Auth.Application.Features.Users.ResetPassword;
using Auth.Domain.Auth.Features.RegisterUser;

namespace Auth.Tests.Users;

/// <summary>LoggingBehavior logs every request via ToString — passwords must never appear in it.</summary>
public class PasswordCommandRedactionTests
{
    private const string Secret = "Hunter2!Secret";

    [Fact]
    public void Password_commands_never_print_the_password()
    {
        object[] commands =
        [
            new ChangePasswordCommand(Guid.NewGuid(), Secret, Secret, Secret),
            new ResetPasswordCommand(Guid.NewGuid(), Secret, Secret),
            new CreateUserCommand("u1", Secret, "u1@x.test", "U", "One", null, null, null, []),
            new RegisterUserCommand("u2", Secret, "u2@x.test", "U", "Two", null, null, null, null, [], [])
        ];

        foreach (var command in commands)
            Assert.DoesNotContain(Secret, command.ToString());
    }
}
