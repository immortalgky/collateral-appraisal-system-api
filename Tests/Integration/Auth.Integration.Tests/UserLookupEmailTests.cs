using Auth.Contracts.Users;
using Auth.Domain;
using Auth.Domain.Groups;
using Auth.Domain.Identity;
using Auth.Domain.Teams;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Auth.Integration.Tests.Bff;
using Integration.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests;

/// <summary>
/// #621: the addresses the assignment email's Cc role chips turn into. Only users who may use the system right now
/// (active, inside any access window) and have an email are returned; only Bank staff in the Bank-scoped group of that
/// name count.
/// Positive cases put test users into the seeded Bank groups. Everything this class creates carries <see cref="Prefix"/>
/// (users, teams) or <see cref="Marker"/> (groups), and is swept before and after each test, so an aborted run leaves nothing behind.
/// </summary>
[Collection("Integration")]
public class UserLookupEmailTests(IntegrationTestFixture fixture) : IAsyncLifetime
{
    private const string Prefix = "ulk";
    private const string Marker = "ulk-test";
    private const string IntAdmin = "IntAdmin";
    private const string Checker = "IntAppraisalChecker";

    private IServiceProvider Services => fixture.IntegrationTestWebApplicationFactory.Services;

    public async ValueTask InitializeAsync() => await CleanupAsync();

    public async ValueTask DisposeAsync() => await CleanupAsync();

    /// <summary>Removes what this class created: memberships, teams, its own groups, then its users.</summary>
    private async Task CleanupAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userIds = await db.Users.Where(u => u.UserName!.StartsWith(Prefix)).Select(u => u.Id).ToListAsync();
        var groupIds = await db.Groups.IgnoreQueryFilters().Where(g => g.Description == Marker).Select(g => g.Id).ToListAsync();
        await db.GroupUsers.Where(gu => userIds.Contains(gu.UserId) || groupIds.Contains(gu.GroupId)).ExecuteDeleteAsync();
        await db.TeamMembers.Where(tm => userIds.Contains(tm.UserId)).ExecuteDeleteAsync();
        await db.Teams.Where(t => t.Name.StartsWith(Prefix)).ExecuteDeleteAsync();
        await db.Groups.IgnoreQueryFilters().Where(g => groupIds.Contains(g.Id)).ExecuteDeleteAsync();
        foreach (var id in userIds)
            if (await users.FindByIdAsync(id.ToString()) is { } user)
                await users.DeleteAsync(user);
    }

    private Task<TestUser> UserAsync(string role = "IntAppraisalStaff", Action<ApplicationUser>? configure = null) =>
        BffTestSupport.CreateUserAsync(Services, role, configure: u =>
        {
            u.UserName = $"{Prefix}{Guid.NewGuid():N}"[..12];
            u.Email = $"{u.UserName}@test.local";
            configure?.Invoke(u);
        });

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

    /// <summary>
    /// Adds <paramref name="members"/> to the seeded Bank group <paramref name="name"/> (created, and swept, if the
    /// database has none).
    /// </summary>
    private async Task<Guid> BankGroupAsync(string name, params TestUser[] members)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Name == name && g.Scope == AuthScopes.Bank);
        if (group is null)
        {
            group = Group.Create(name, Marker, AuthScopes.Bank);
            db.Groups.Add(group);
        }

        foreach (var m in members)
            db.GroupUsers.Add(new GroupUser { GroupId = group.Id, UserId = m.Id });
        await db.SaveChangesAsync();
        return group.Id;
    }

    /// <summary>A group of its own that shares the name of the real one (Groups are unique on Name+Scope), swept afterwards.</summary>
    private async Task<Guid> OtherGroupAsync(string name, string scope, bool deleted, Guid? companyId, params TestUser[] members)
    {
        await using var scopeServices = Services.CreateAsyncScope();
        var db = scopeServices.ServiceProvider.GetRequiredService<AuthDbContext>();
        var group = Group.Create(name, Marker, scope, companyId);
        if (deleted) group.Delete(null);
        db.Groups.Add(group);
        foreach (var m in members)
            db.GroupUsers.Add(new GroupUser { GroupId = group.Id, UserId = m.Id });
        await db.SaveChangesAsync();
        return group.Id;
    }

    private Task<Guid> TeamAsync(params TestUser[] members) => TeamAsync(AuthScopes.Bank, members);

    private async Task<Guid> TeamAsync(string teamScope, params TestUser[] members)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var team = Team.Create($"{Prefix}t{Guid.NewGuid():N}"[..20], teamScope);
        db.Teams.Add(team);
        foreach (var m in members)
            db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = m.Id });
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task<IReadOnlyList<string>> Lookup(Func<IUserLookupService, Task<IReadOnlyList<string>>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IUserLookupService>());
    }

    private async Task<string[]> UsernamesIn(string group)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserLookupService>()
            .GetUsernamesInGroupAsync(group, CancellationToken.None);
    }

    private Task<IReadOnlyList<string>> EmailsIn(string group) =>
        Lookup(s => s.GetEmailsInGroupAsync(group, CancellationToken.None));

    private async Task<IReadOnlyList<string>> SupervisorsOf(TestUser user) =>
        await Lookup(s => s.GetSupervisorEmailsAsync(user.UserName, CancellationToken.None));

    [Fact]
    public async Task Emails_in_a_group_are_those_of_its_usable_members_with_an_email()
    {
        var active = await UserAsync();
        var windowOpen = await UserAsync(configure: u => u.AccessExpiresAt = DateTime.Now.AddDays(1));
        var inactive = await UserAsync(configure: u => u.IsActive = false);
        var expired = await UserAsync(configure: u => u.AccessExpiresAt = DateTime.Now.AddDays(-1));
        var noEmail = await UserAsync();
        var blankEmail = await UserAsync();
        var notInGroup = await UserAsync();
        var holdsRoleOnly = await UserAsync(IntAdmin); // the IntAdmin role is a permission, not membership of the group
        var (activeEmail, windowEmail, inactiveEmail, expiredEmail, otherEmail, roleEmail) = (
            await EmailOf(active), await EmailOf(windowOpen), await EmailOf(inactive), await EmailOf(expired),
            await EmailOf(notInGroup), await EmailOf(holdsRoleOnly));
        await ClearEmailAsync(noEmail, null);
        await ClearEmailAsync(blankEmail, "  ");
        await BankGroupAsync(IntAdmin, active, windowOpen, inactive, expired, noEmail, blankEmail);

        var emails = await EmailsIn(IntAdmin);

        emails.Should().Contain([activeEmail, windowEmail]);
        emails.Should().NotContain([inactiveEmail, expiredEmail, otherEmail, roleEmail]);
        emails.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e));
    }

    [Fact]
    public async Task Usernames_in_a_group_are_those_of_its_usable_members()
    {
        var active = await UserAsync();
        var windowOpen = await UserAsync(configure: u => u.AccessExpiresAt = DateTime.Now.AddDays(1));
        var inactive = await UserAsync(configure: u => u.IsActive = false);
        var expired = await UserAsync(configure: u => u.AccessExpiresAt = DateTime.Now.AddDays(-1));
        var notInGroup = await UserAsync();
        await BankGroupAsync(IntAdmin, active, windowOpen, inactive, expired);

        var names = await UsernamesIn(IntAdmin);

        names.Should().Contain([active.UserName, windowOpen.UserName]);
        names.Should().NotContain([inactive.UserName, expired.UserName, notInGroup.UserName]);
    }

    [Fact]
    public async Task A_deleted_group_gives_nothing()
    {
        var member = await UserAsync();
        await OtherGroupAsync(IntAdmin, AuthScopes.Bank, deleted: true, null, member); // coexists with the live one

        (await EmailsIn(IntAdmin)).Should().NotContain(await EmailOf(member));
        (await UsernamesIn(IntAdmin)).Should().NotContain(member.UserName);
    }

    [Fact]
    public async Task A_company_scoped_group_of_the_same_name_gives_nothing()
    {
        var member = await UserAsync();
        await OtherGroupAsync(IntAdmin, AuthScopes.Company, deleted: false, null, member);

        (await EmailsIn(IntAdmin)).Should().NotContain(await EmailOf(member));
        (await UsernamesIn(IntAdmin)).Should().NotContain(member.UserName);
    }

    [Fact]
    public async Task A_bank_group_owned_by_a_company_gives_nothing()
    {
        // The seeded Bank group already holds the real name, so use a name of its own: the lookup takes any name.
        var name = $"{Prefix}g{Guid.NewGuid():N}"[..16];
        var ownedByCompany = await UserAsync();
        var plain = await UserAsync();
        await OtherGroupAsync(name, AuthScopes.Bank, deleted: false, Guid.NewGuid(), ownedByCompany);
        await OtherGroupAsync($"{name}b", AuthScopes.Bank, deleted: false, null, plain);

        (await EmailsIn(name)).Should().BeEmpty();
        (await UsernamesIn(name)).Should().BeEmpty();
        (await EmailsIn($"{name}b")).Should().Equal(await EmailOf(plain)); // the control: same shape, no company
    }

    [Fact]
    public async Task A_company_user_in_the_bank_group_is_left_out()
    {
        var bank = await UserAsync();
        var company = await UserAsync(configure: u => u.CompanyId = Guid.NewGuid());
        await BankGroupAsync(IntAdmin, bank, company);

        var emails = await EmailsIn(IntAdmin);
        var names = await UsernamesIn(IntAdmin);

        emails.Should().Contain(await EmailOf(bank)).And.NotContain(await EmailOf(company));
        names.Should().Contain(bank.UserName).And.NotContain(company.UserName);
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
    public async Task Supervisors_are_the_checkers_who_share_a_team_with_the_user()
    {
        var staff = await UserAsync();
        var checker = await UserAsync();
        var otherTeamChecker = await UserAsync();
        var teammate = await UserAsync(); // same team, but not in the checker group
        await TeamAsync(staff, checker, teammate);
        await TeamAsync(otherTeamChecker);
        await BankGroupAsync(Checker, checker, otherTeamChecker);

        (await SupervisorsOf(staff)).Should().Equal(await EmailOf(checker));
    }

    [Fact]
    public async Task A_user_in_two_teams_gets_the_checkers_of_both()
    {
        var staff = await UserAsync();
        var checkerA = await UserAsync();
        var checkerB = await UserAsync();
        var checkerBoth = await UserAsync(); // in both teams: listed once
        await TeamAsync(staff, checkerA, checkerBoth);
        await TeamAsync(staff, checkerB, checkerBoth);
        await BankGroupAsync(Checker, checkerA, checkerB, checkerBoth);

        (await SupervisorsOf(staff)).Should().BeEquivalentTo(
            [await EmailOf(checkerA), await EmailOf(checkerB), await EmailOf(checkerBoth)]);
    }

    [Fact]
    public async Task Supervisors_leave_out_inactive_expired_and_emailless_checkers()
    {
        var staff = await UserAsync();
        var inactive = await UserAsync(configure: u => u.IsActive = false);
        var expired = await UserAsync(configure: u => u.AccessExpiresAt = DateTime.Now.AddDays(-1));
        var noEmail = await UserAsync();
        await ClearEmailAsync(noEmail, null);
        await TeamAsync(staff, inactive, expired, noEmail);
        await BankGroupAsync(Checker, inactive, expired, noEmail);

        (await SupervisorsOf(staff)).Should().BeEmpty();
    }

    [Fact]
    public async Task Supervisors_come_only_from_the_live_bank_checker_group_and_bank_staff()
    {
        var staff = await UserAsync();
        var inDeleted = await UserAsync();
        var inCompanyGroup = await UserAsync();
        var companyUser = await UserAsync(configure: u => u.CompanyId = Guid.NewGuid());
        await TeamAsync(staff, inDeleted, inCompanyGroup, companyUser);
        await OtherGroupAsync(Checker, AuthScopes.Bank, deleted: true, null, inDeleted);
        await OtherGroupAsync(Checker, AuthScopes.Company, deleted: false, null, inCompanyGroup);
        await BankGroupAsync(Checker, companyUser);

        (await SupervisorsOf(staff)).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_a_bank_team_makes_a_checker_a_supervisor()
    {
        var staff = await UserAsync();
        var checker = await UserAsync();
        await TeamAsync(AuthScopes.Company, staff, checker);
        await BankGroupAsync(Checker, checker);

        (await SupervisorsOf(staff)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_user_without_a_team_has_no_supervisors()
    {
        var alone = await UserAsync();
        var checker = await UserAsync();
        await TeamAsync(checker);
        await BankGroupAsync(Checker, checker);

        (await SupervisorsOf(alone)).Should().BeEmpty();
    }

    [Fact]
    public async Task Names_by_email_are_first_and_last_name_else_the_username_for_usable_users_only()
    {
        var named = await UserAsync(configure: u => (u.FirstName, u.LastName) = ("สมหญิง", "แอดมิน"));
        var unnamed = await UserAsync(configure: u => (u.FirstName, u.LastName) = ("", ""));
        var inactive = await UserAsync(configure: u => u.IsActive = false);
        var (namedEmail, unnamedEmail, inactiveEmail) = (await EmailOf(named), await EmailOf(unnamed), await EmailOf(inactive));

        await using var scope = Services.CreateAsyncScope();
        var names = await scope.ServiceProvider.GetRequiredService<IUserLookupService>().GetNamesByEmailAsync(
            [namedEmail.ToUpperInvariant(), unnamedEmail, inactiveEmail, "nobody@here.test"], CancellationToken.None);

        names.Should().HaveCount(2);
        names[namedEmail].Should().Be("สมหญิง แอดมิน");
        names[unnamedEmail].Should().Be(unnamed.UserName);
    }
}
