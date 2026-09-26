using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Exceptions.Handler;
using Shared.Security;

namespace Shared.Tests.Exceptions;

/// <summary>Error responses must never carry a secret or certificate details back to the browser.</summary>
public class CustomExceptionHandlerSecretTests
{
    private static async Task<string> BodyFor(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await new CustomExceptionHandler(NullLogger<CustomExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    [Fact]
    public async Task A_validation_failure_does_not_echo_the_rejected_value()
    {
        var failure = new ValidationFailure("ClientSecret", "Too long.") { AttemptedValue = "s3cret-value" };

        var body = await BodyFor(new ValidationException([failure]));

        body.Should().Contain("ClientSecret", "the caller still needs to know which field failed")
            .And.NotContain("s3cret-value");
    }

    [Fact]
    public async Task A_cipher_failure_answers_with_a_fixed_message()
    {
        var body = await BodyFor(new SecretCipherException("Certificate THUMB123 in LocalMachine\\My has no key"));

        body.Should().Contain("Secret storage is unavailable").And.NotContain("THUMB123");
    }
}
