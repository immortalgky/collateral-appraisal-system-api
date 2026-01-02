using Request.Domain.Requests;
using Request.Tests.TestData;

namespace Request.Tests.Request.Requests.Models;

public class RequestTests
{
    [Fact]
    public void SetCustomers_WithUniqueCustomers_ShouldPass()
    {
        var request = ModelsTestData.RequestGeneral();
        var customers = new List<RequestCustomer>
        {
            RequestCustomer.Create("Dave", "0123456789")
        };
        request.SetCustomers(customers);
        Assert.Single(request.Customers);
    }

    [Fact]
    public void SetCustomers_WithDuplicateNames_ShouldFail()
    {
        var request = ModelsTestData.RequestGeneral();
        var customers = new List<RequestCustomer>
        {
            RequestCustomer.Create("Dave", "0123456789"),
            RequestCustomer.Create("Dave", "0987654321")
        };
        Assert.Throws<ArgumentException>(() => request.SetCustomers(customers));
    }

    [Fact]
    public void SetCustomers_WithEmptyList_ShouldClearCustomers()
    {
        var request = ModelsTestData.RequestGeneral();
        var customers = new List<RequestCustomer>
        {
            RequestCustomer.Create("Dave", "0123456789")
        };
        request.SetCustomers(customers);
        request.SetCustomers([]);
        Assert.Empty(request.Customers);
    }

    [Fact]
    public void SetProperties_WithUniqueProperties_ShouldPass()
    {
        var request = ModelsTestData.RequestGeneral();
        var properties = new List<RequestProperty>
        {
            RequestProperty.Create("Condo", "Condo", 1),
            RequestProperty.Create("Condo", "House", 1)
        };
        request.SetProperties(properties);
        Assert.Equal(2, request.Properties.Count);
    }

    [Fact]
    public void SetProperties_WithDuplicateTypeAndBuilding_ShouldFail()
    {
        var request = ModelsTestData.RequestGeneral();
        var properties = new List<RequestProperty>
        {
            RequestProperty.Create("Condo", "Condo", 1),
            RequestProperty.Create("Condo", "Condo", 2)
        };
        Assert.Throws<ArgumentException>(() => request.SetProperties(properties));
    }

    [Fact]
    public void SetProperties_WithEmptyList_ShouldClearProperties()
    {
        var request = ModelsTestData.RequestGeneral();
        var properties = new List<RequestProperty>
        {
            RequestProperty.Create("Condo", "Condo", 1)
        };
        request.SetProperties(properties);
        request.SetProperties([]);
        Assert.Empty(request.Properties);
    }
}