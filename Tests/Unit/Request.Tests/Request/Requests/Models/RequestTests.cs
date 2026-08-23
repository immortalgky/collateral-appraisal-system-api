using Request.Domain.Requests;
using Request.Tests.TestData;
using Shared.Exceptions;

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

    [Fact]
    public void MarkAsNew_OnADraftRequest_ShouldPromoteToNew()
    {
        var request = ModelsTestData.RequestGeneral();

        request.MarkAsNew();

        Assert.Equal(RequestStatus.New, request.Status);
    }

    [Fact]
    public void MarkAsNew_AfterSubmit_ShouldBeANoOp()
    {
        // Regression: a post-submit save used to demote the request back into the intake
        // listing, which defaults to Status IN ('Draft','New').
        var request = ModelsTestData.RequestGeneral();
        request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        request.MarkAsNew();

        Assert.Equal(RequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void MarkAsDraft_AfterSubmit_ShouldThrow()
    {
        var request = ModelsTestData.RequestGeneral();
        request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        Assert.Throws<DomainException>(() => request.MarkAsDraft());
        Assert.Equal(RequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void Delete_BeforeSubmit_ShouldSoftDelete()
    {
        var request = ModelsTestData.RequestGeneral();

        request.Delete("01", new DateTime(2026, 8, 21, 9, 0, 0));

        Assert.True(request.SoftDelete.IsDeleted);
    }

    [Fact]
    public void Delete_AfterSubmit_ShouldThrow()
    {
        // Deleting a submitted request orphans its appraisal task: the task stays in the
        // task list but the request starts returning 404.
        var request = ModelsTestData.RequestGeneral();
        request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        Assert.Throws<DomainException>(() => request.Delete("01", new DateTime(2026, 8, 21, 10, 0, 0)));
        Assert.False(request.SoftDelete.IsDeleted);
    }

    [Fact]
    public void Submit_Twice_ShouldThrow()
    {
        var request = ModelsTestData.RequestGeneral();
        request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        Assert.Throws<DomainException>(() => request.Submit(new DateTime(2026, 8, 21, 10, 0, 0)));
    }

    [Fact]
    public void HasBeenSubmitted_ShouldFollowTheSubmitBoundary()
    {
        var request = ModelsTestData.RequestGeneral();
        Assert.False(request.HasBeenSubmitted());

        request.Submit(new DateTime(2026, 8, 21, 9, 0, 0));

        Assert.True(request.HasBeenSubmitted());
    }
}
