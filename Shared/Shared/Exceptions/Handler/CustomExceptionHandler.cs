using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.Exceptions.Handler;

public class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError("An error occurred: {Message} {Time}", exception.Message, DateTime.Now);

        var (detail, title, statusCode) = exception switch
        {
            BadHttpRequestException badHttpEx =>
            (
                GetFriendlyDeserializationMessage(badHttpEx),
                "InvalidRequestFormat",
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            BadRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            ValidationException =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            NotFoundException =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound
            ),
            ConflictException =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict
            ),
            UnauthorizedAccessException =>
            (
                "Access is denied",
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden
            ),
            OperationCanceledException =>
            (
                "The request was cancelled",
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest
            ),
            DbUpdateException =>
            (
                "A database error occurred. Please contact support",
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError
            ),
            TimeoutException =>
            (
                "The operation timed out. Please try again",
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status504GatewayTimeout
            ),
            _ =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError
            )
        };

        var problemDetails = new ProblemDetails
        {
            Detail = detail,
            Title = title,
            Status = statusCode,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions.Add("traceId", httpContext.TraceIdentifier);

        if (exception is ValidationException validationException)
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static string GetFriendlyDeserializationMessage(BadHttpRequestException ex)
    {
        if (ex.InnerException is JsonException jsonEx && jsonEx.Path is not null)
        {
            return $"Invalid value at '{jsonEx.Path}'. Please check the data type and format.";
        }

        return "Invalid request format. Please check your input data types and try again.";
    }
}