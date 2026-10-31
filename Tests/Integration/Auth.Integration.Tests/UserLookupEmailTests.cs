using Auth.Contracts.Users;
using Auth.Domain.Groups;
using Auth.Domain.Identity;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Auth.Integration.Tests.Bff;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests;

/// <summary>
/// #621: the addresses the assignment email's Cc role chips turn into. Only users who may use the system right now
/// (active, inside any access window) and have an email are returned.
/// </summary>
[Collection("Integration")]
public class UserLookupEmailTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.IntegrationTestWebApplicationFactory.Services;

    private Task<TestUser> UserAsync(string role = "IntAppraisalStaff", Action<ApplicationUser>? configure = null) =>
        BffTestSupport.CreateUserAsync(Services, role, configure: configure);

    private async Task<string> EmailOf(TestUser user)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Email!;
    }

    private async Task ClearEmailAsync(TestUser user, string? blank)
    {
        // RequireUniqueEmail refuses a blank email at create, but legacy and LDAP rows hold one.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.Email, blank)
            .SetProperty(u => u.NormalizedEmail, (string?)null));
    }

    private async Task<Guid> GroupAsync(string? name, params TestUser[] members)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var group = name is null
            ? null
            : await db.Groups.FirstOrDefaultAsync(g => g.Name == name && !g.IsDeleted);
        if (group is null)
        {
            group = Group.Create(name ?? $"g{Guid.NewGuid():N}"[..20], "test", "Bank");
            db.Groups.Add(group);
        }

        foreach (var m in members)
            db.GroupUsers.Add(new GroupUser { GroupId = group.Id, UserId = m.Id });
        await db.SaveChangesAsync();
        return group.Id;
    }

    private async Task MonitorAsync(Guid monitorGroupId, Guid monitoredGroupId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.GroupMonitoring.Add(new GroupMonitoring { MonitorGroupId = monitorGroupId, MonitoredGroupId = monitoredGroupId });
        await db.SaveChangesAsync();
    }

    private async Task DeleteGroupAsync(Guid groupId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        (await db.Groups.SingleAsync(g => g.Id == groupId)).Delete(null);
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> Lookup(Func<IUserLookupService, Task<IReadOnlyList<string>>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IUserLookupService>());
    }

    [Fact]
    public async Task Emails_in_a_role_are_those_of_its_usable_users_with_an_email()
    {
        var active = await UserAsync("IntAdmin");
        var windowOpen = await UserAsync("IntAdmin", u => u.AccessExpiresAt = DateTime.Now.AddDays(1));
        var inactive = await UserAsync("IntAdmin", u => u.IsActive = false);
        var expired = await UserAsync("IntAdmin", u => u.AccessExpiresAt = DateTime.Now.AddDays(-1));
        var noEmail = await UserAsync("IntAdmin");
        var blankEmail = await UserAsync("IntAdmin");
        var otherRole = await UserAsync("IntAppraisalStaff");
        var (activeEmail, windowEmail, inactiveEmail, expiredEmail, otherEmail) = (
            await EmailOf(active), await EmailOf(windowOpen), await EmailOf(inactive), await EmailOf(expired),
            await EmailOf(otherRole));
        await ClearEmailAsync(noEmail, null);
        await ClearEmailAsync(blankEmail, "  ");

        var emails = await Lookup(s => s.GetEmailsInRoleAsync("IntAdmin", CancellationToken.None));

        emails.Should().Contain([activeEmail, windowEmail]);
        emails.Should().NotContain([inactiveEmail, expiredEmail, otherEmail]);
        emails.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e));
    }

    [Fact]
    public async Task Emails_of_users_are_those_of_the_usable_ones()
    {
        var active = await UserAsync();
        var inactive = await UserAsync(configure: u => u.IsActive = false);
        var expired = await UserAsync(configure: u => u.AccessExpiresAt = DateTime.Now.AddDays(-1));
        var noEmail = await UserAsync();
        await ClearEmailAsync(noEmail, null);

        var emails = await Lookup(s => s.GetEmailsAsync(
            [active.UserName.ToUpperInvariant(), inactive.UserName, expired.UserName, noEmail.UserName, "nobody-here"],
            CancellationToken.None));

        emails.Should().Equal(await EmailOf(active));
    }

    [Fact]
    public async Task Supervisors_are_the_usable_members_of_groups_monitoring_any_of_the_users_groups()
    {
        var keyer = await UserAsync("IntAdmin");
        var lead = await UserAsync();
        var manager = await UserAsync(); // in two monitoring groups: listed once
        var inactiveLead = await UserAsync(configure: u => u.IsActive = false);
        var deletedGroupLead = await UserAsync();
        var bystander = await UserAsync();
        var intAdmin = await UserAsync("IntAdmin");

        var keyGroup = await GroupAsync(null, keyer);
        var otherGroup = await GroupAsync(null, keyer);
        await MonitorAsync(await GroupAsync(null, lead, manager, inactiveLead), keyGroup);
        await MonitorAsync(await GroupAsync(null, manager), otherGroup);
        var deleted = await GroupAsync(null, deletedGroupLead);
        await MonitorAsync(deleted, keyGroup);
        await DeleteGroupAsync(deleted);
        await GroupAsync(null, bystander); // monitors nobody
        // The IntAdmin group watches everyone's tasks: it is Cc'd through its own chip, never as a supervisor.
        await MonitorAsync(await GroupAsync("IntAdmin", intAdmin), keyGroup);
        // The keyer's own IntAdmin membership still counts: whoever monitors the IntAdmin group supervises them.
        var adminHead = await UserAsync();
        await MonitorAsync(await GroupAsync(null, adminHead), await GroupAsync("IntAdmin", keyer));

        var emails = await Lookup(s => s.GetSupervisorEmailsAsync(keyer.UserName, CancellationToken.None));

        emails.Should().BeEquivalentTo([await EmailOf(lead), await EmailOf(manager), await EmailOf(adminHead)]);
    }

    [Fact]
    public async Task A_user_without_a_monitored_group_has_no_supervisors()
    {
        var alone = await UserAsync();

        (await Lookup(s => s.GetSupervisorEmailsAsync(alone.UserName, CancellationToken.None))).Should().BeEmpty();
    }
}
