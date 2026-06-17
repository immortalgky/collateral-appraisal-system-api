using Auth.Domain.Companies;

namespace Auth.Tests.Domain;

public class CompanyIsAssignableTests
{
    private static readonly DateTime AsOf = new(2026, 6, 14, 10, 30, 0);

    [Fact]
    public void NullDates_AreAlwaysAssignable()
    {
        var company = Company.Create("Acme");
        Assert.True(company.IsAssignable(AsOf));
    }

    [Fact]
    public void ExpireDateToday_IsStillAssignable_ThroughTheWholeDay()
    {
        // ExpireDate stored at midnight of the same calendar day as AsOf (which is mid-morning).
        var company = Company.Create("Acme", expireDate: new DateTime(2026, 6, 14, 0, 0, 0));
        Assert.True(company.IsAssignable(AsOf));
    }

    [Fact]
    public void ExpiredYesterday_IsNotAssignable()
    {
        var company = Company.Create("Acme", expireDate: new DateTime(2026, 6, 13, 0, 0, 0));
        Assert.False(company.IsAssignable(AsOf));
    }

    [Fact]
    public void EffectiveToday_IsAssignable()
    {
        var company = Company.Create("Acme", effectiveDate: new DateTime(2026, 6, 14, 0, 0, 0));
        Assert.True(company.IsAssignable(AsOf));
    }

    [Fact]
    public void EffectiveTomorrow_IsNotAssignable()
    {
        var company = Company.Create("Acme", effectiveDate: new DateTime(2026, 6, 15, 0, 0, 0));
        Assert.False(company.IsAssignable(AsOf));
    }
}
