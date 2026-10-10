using Appraisal.Application.Features.Appraisals.GetAppraisalById;
using FluentAssertions;
using Integration.Application.Features.Appraisals.GetAppraisalById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Exceptions;

namespace Integration.Tests;

/// <summary>
/// LOS reads an appraisal (and, opt in, its request data and files) on the /api/v1 prefix under the Integration
/// policy. It sends the very query the Appraisal module's GET /appraisals/{id} sends - no logic of its own.
/// </summary>
public class GetAppraisalByIdIntegrationEndpointTests
{
    private static (RouteEndpoint Endpoint, ISender Sender, IServiceProvider Services) Map()
    {
        var sender = Substitute.For<ISender>();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(sender);
        var app = builder.Build();
        new GetAppraisalByIdIntegrationEndpoint().AddRoutes(app);

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().Single();
        return (endpoint, sender, app.Services);
    }

    private static async Task<DefaultHttpContext> Call(
        RouteEndpoint endpoint, IServiceProvider services, Guid id, string? include)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.RouteValues["appraisalId"] = id.ToString();
        if (include is not null) context.Request.QueryString = new QueryString($"?include={include}");
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    [Fact]
    public void The_route_is_under_api_v1_and_needs_the_Integration_policy()
    {
        var (endpoint, _, _) = Map();

        endpoint.RoutePattern.RawText.Should().Be("/api/v1/appraisals/{appraisalId:guid}");
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Should().BeEquivalentTo("GET");
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Should().ContainSingle(a => a.Policy == "Integration");
        endpoint.Metadata.GetMetadata<IAllowAnonymous>().Should().BeNull();
    }

    [Theory]
    [InlineData(null, AppraisalInclude.None)]
    [InlineData("request", AppraisalInclude.Request)]
    [InlineData("documents", AppraisalInclude.Documents)]
    [InlineData("request,documents", AppraisalInclude.Request | AppraisalInclude.Documents)]
    public async Task It_sends_the_Appraisal_modules_query_with_the_parsed_include(string? include, AppraisalInclude expected)
    {
        var (endpoint, sender, services) = Map();
        var id = Guid.NewGuid();
        sender.Send(Arg.Any<GetAppraisalByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetAppraisalByIdResult { Id = id, Status = "Completed", AppraisalType = "New", Priority = "Normal" });

        var context = await Call(endpoint, services, id, include);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        await sender.Received(1).Send(
            Arg.Is<GetAppraisalByIdQuery>(q => q.Id == id && q.Include == expected && q.StrictRelease), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_include_is_a_400_and_nothing_is_read()
    {
        var (endpoint, sender, services) = Map();

        var act = () => Call(endpoint, services, Guid.NewGuid(), "titles");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*titles*");
        await sender.DidNotReceiveWithAnyArgs().Send<GetAppraisalByIdResult>(default!, default);
    }
}
