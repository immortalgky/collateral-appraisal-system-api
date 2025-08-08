using Request.Tests.TestData;
using Shared.Exceptions;

namespace Request.Tests.Request.Requests.Models;

public class RequestTests
{
    [Fact]
    public void AddCustomer_CustomerDoesNotExists_ShouldPass()
    {
        var request = ModelsTestData.RequestGeneral();
        request.AddCustomer("Dave", "0123456789");
        Assert.Single(request.Customers);
    }

    [Fact]
    public void AddCustomer_CustomerNameExists_ShouldFail()
    {
        var request = ModelsTestData.RequestGeneral();
        request.AddCustomer("Dave", "0123456789");
        Assert.Throws<DomainException>(() => request.AddCustomer("Dave", "0123456789"));
    }

    [Fact]
    public void RemoveCustomer_CustomerNameExists_ShouldPass()
    {
        var request = ModelsTestData.RequestGeneral();
        request.AddCustomer("Dave", "0123456789");
        request.RemoveCustomer("Dave");
        Assert.False(request.Customers.Any());
    }

    [Fact]
    public void RemoveCustomer_CustomerNameDoesNotExist_ShouldFail()
    {
        var request = ModelsTestData.RequestGeneral();
        Assert.Throws<DomainException>(() => request.RemoveCustomer("Dave"));
    }

    [Fact]
    public void AddProperty_PropertyTypeOrBuildingTypeDoNotExist_ShouldPass()
    {
        var request = ModelsTestData.RequestGeneral();
        request.AddProperty("Condo", "Condo", 1);
        request.AddProperty("Condo", "House", 1);
        Assert.Equal(2, request.Properties.Count);
    }

    [Fact]
    public void AddProperty_PropertyTypeAndBuildingTypeExist_ShouldFail()
    {
        var request = ModelsTestData.RequestGeneral();
        request.AddProperty("Condo", "Condo", 1);
        Assert.Throws<DomainException>(() => request.AddProperty("Condo", "Condo", 2));
    }

    [Fact]
    public void RemoveProperty_PropertyTypeAndBuildingTypeExist_ShouldPass()
    {
        var request = ModelsTestData.RequestGeneral();
        request.AddProperty("Condo", "Condo", 1);
        request.RemoveProperty("Condo", "Condo");
        Assert.False(request.Properties.Any());
    }

    [Fact]
    public void RemoveProperty_PropertyTypeAndBuildingTypeDoNotExist_ShouldFail()
    {
        var request = ModelsTestData.RequestGeneral();
        Assert.Throws<DomainException>(() => request.RemoveProperty("Condo", "Condo"));
    }

    [Fact]
    public void UpdateComment_CommentIdExists_ShouldPass()
    {
        // var request = ModelsTestData.RequestGeneral();
        // request.AddComment("Original.");
        // var commentId = request.Comments[0].Id;
        // request.UpdateComment(commentId, "Edited");
        // var comment = request.Comments.FirstOrDefault(c => c.Id == commentId);
        // Assert.NotNull(comment);
        // Assert.Equal("Edited", comment.Comment);
    }

    [Fact]
    public void UpdateComment_CommentIdDoesNotExist_ShouldFail()
    {
        // var request = ModelsTestData.RequestGeneral();
        // Assert.Throws<NotFoundException>(() => request.UpdateComment(0, "Edited"));
    }
}