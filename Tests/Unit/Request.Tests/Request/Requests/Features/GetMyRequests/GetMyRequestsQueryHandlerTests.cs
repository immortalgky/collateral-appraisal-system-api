using System.Reflection;
using NSubstitute;
using Request.Application.Features.Requests.GetMyRequests;
using Request.Application.Features.Requests.GetRequests;
using Request.Infrastructure;
using Shared.Identity;

namespace Request.Tests.Request.Requests.Features.GetMyRequests;

public class GetMyRequestsQueryHandlerTests
{
    [Fact]
    public void GetMyRequestsQueryHandler_ShouldRequireCurrentUserService()
    {
        // The GetMyRequestsQueryHandler constructor requires ICurrentUserService
        var ctors = typeof(GetMyRequestsQueryHandler).GetConstructors();
        var ctor = ctors.Single();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(ICurrentUserService), paramTypes);
        Assert.Contains(typeof(RequestDbContext), paramTypes);
    }

    [Fact]
    public void GetRequestQueryHandler_ShouldNotRequireCurrentUserService()
    {
        // After the change, GetRequestQueryHandler should NOT inject ICurrentUserService
        var ctors = typeof(GetRequestQueryHandler).GetConstructors();
        var ctor = ctors.Single();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        Assert.DoesNotContain(typeof(ICurrentUserService), paramTypes);
        Assert.Contains(typeof(RequestDbContext), paramTypes);
    }

    [Fact]
    public void GetMyRequestsQuery_ShouldHaveSameParametersAsGetRequestQuery()
    {
        // Both queries should accept the same filter params
        var myProps = typeof(GetMyRequestsQuery).GetProperties()
            .Select(p => p.Name).OrderBy(n => n).ToList();
        var allProps = typeof(GetRequestQuery).GetProperties()
            .Select(p => p.Name).OrderBy(n => n).ToList();

        Assert.Equal(allProps, myProps);
    }

    [Fact]
    public void GetMyRequestsResult_ShouldReuseSameDtoAsGetRequestResult()
    {
        // GetMyRequestsResult wraps PaginatedResult<GetRequestListItem> — same DTO type
        var resultType = typeof(GetMyRequestsResult);
        var resultProp = resultType.GetProperty("Result");
        Assert.NotNull(resultProp);

        var genericArgs = resultProp!.PropertyType.GetGenericArguments();
        Assert.Single(genericArgs);
        Assert.Equal(typeof(GetRequestListItem), genericArgs[0]);
    }
}
