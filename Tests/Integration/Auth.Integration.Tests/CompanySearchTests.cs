using Auth.Domain.Companies;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests;

/// <summary>The company list search (the email drawer's recipient search) also matches a company's email and contact person.</summary>
[Collection("Integration")]
public class CompanySearchTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Search_matches_part_of_the_email_or_the_contact_person_besides_name_and_tax_id()
    {
        var run = Guid.NewGuid().ToString("N")[..10];
        var byEmail = Company.Create($"Co A {run}", email: $"jobs@kawin{run}.co.th; second@x.test");
        var byContact = Company.Create($"Co B {run}", contactPerson: $"คุณสมศรี{run}");
        var unrelated = Company.Create($"Co C {run}", email: "other@elsewhere.test");

        await using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Companies.AddRange(byEmail, byContact, unrelated);
        await db.SaveChangesAsync();
        var repository = scope.ServiceProvider.GetRequiredService<ICompanyRepository>();

        var email = await repository.SearchAsync($"kawin{run}", activeOnly: true);
        var contact = await repository.SearchAsync($"สมศรี{run}", activeOnly: true);
        var name = await repository.SearchAsync($"Co C {run}", activeOnly: true);

        email.Select(c => c.Id).Should().Equal(byEmail.Id);
        contact.Select(c => c.Id).Should().Equal(byContact.Id);
        name.Select(c => c.Id).Should().Equal(unrelated.Id);
    }
}
