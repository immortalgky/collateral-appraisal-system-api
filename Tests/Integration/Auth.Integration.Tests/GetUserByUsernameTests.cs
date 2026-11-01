using Auth.Application.Features.Users.GetUserByUsername;
using FluentAssertions;
using Auth.Domain.Identity;
using Integration.Auth.Integration.Tests.Bff;
using Integration.Fixtures;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;

namespace Integration.Auth.Integration.Tests;

/// <summary>GET /auth/users/by-username/{username} returns the contact card of the user with that exact username.</summary>
[Collection("Integration")]
public class GetUserByUsernameTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task The_user_is_found_by_username_whatever_the_casing_and_a_partial_name_is_a_404()
    {
        var user = await BffTestSupport.CreateUserAsync(fixture.IntegrationTestWebApplicationFactory.Services, "IntAppraisalStaff");
        await using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var found = await sender.Send(new GetUserByUsernameQuery(user.UserName.ToUpperInvariant()), TestContext.Current.CancellationToken);

        found.Id.Should().Be(user.Id);
        found.Username.Should().Be(user.UserName);
        found.FirstName.Should().Be("Bff");
        found.Email.Should().Be($"{user.UserName}@test.local");
        // The contact card only: nothing of the admin detail.
        var json = System.Text.Json.JsonSerializer.Serialize(found, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        json.Should().NotContainEquivalentOf("roles").And.NotContainEquivalentOf("permissions")
            .And.NotContainEquivalentOf("groups").And.NotContainEquivalentOf("teams");
        typeof(UserContactDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            ["Id", "Username", "FirstName", "LastName", "Email", "PhoneNumber", "Position", "Department", "AvatarUrl"]);

        var partial = () => sender.Send(new GetUserByUsernameQuery(user.UserName[..^1]), TestContext.Current.CancellationToken);
        await partial.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task A_system_account_is_a_404_but_an_inactive_user_is_found()
    {
        var services = fixture.IntegrationTestWebApplicationFactory.Services;
        var system = await BffTestSupport.CreateUserAsync(services, "IntAppraisalStaff", configure: u => u.IsSystem = true);
        var inactive = await BffTestSupport.CreateUserAsync(services, "IntAppraisalStaff", configure: u => u.IsActive = false);
        await using var scope = services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var hidden = () => sender.Send(new GetUserByUsernameQuery(system.UserName), TestContext.Current.CancellationToken);
        await hidden.Should().ThrowAsync<NotFoundException>();

        (await sender.Send(new GetUserByUsernameQuery(inactive.UserName), TestContext.Current.CancellationToken))
            .Id.Should().Be(inactive.Id);
    }

    [Fact]
    public async Task A_company_user_is_a_404_as_a_target_and_a_403_as_the_caller()
    {
        var services = fixture.IntegrationTestWebApplicationFactory.Services;
        var staff = await BffTestSupport.CreateUserAsync(services, "IntAppraisalStaff");
        var companyUser = await BffTestSupport.CreateUserAsync(services, "ExtAppraisalStaff", companyId: Guid.NewGuid());
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bank = Substitute.For<ICurrentUserService>();
        var company = Substitute.For<ICurrentUserService>();
        company.IsExternal.Returns(true);
        var ct = TestContext.Current.CancellationToken;

        var target = () => new GetUserByUsernameQueryHandler(users, bank).Handle(new GetUserByUsernameQuery(companyUser.UserName), ct);
        await target.Should().ThrowAsync<NotFoundException>();

        var caller = () => new GetUserByUsernameQueryHandler(users, company).Handle(new GetUserByUsernameQuery(staff.UserName), ct);
        await caller.Should().ThrowAsync<ForbiddenException>();
    }
}
