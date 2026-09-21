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
        logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        var (detail, title, statusCode) = exception switch
        {
            // ReadFormAsync throws this when a FormOptions limit is exceeded — the multipart body,
            // a single value over ValueLengthLimit, more fields than ValueCountLimit — and, since
            // the Excel importers parse inside the request, for a corrupt or truncated workbook
            // too. Hence "could not be read" rather than naming a cause: blaming size for a damaged
            // file would send the user to shrink something that is not too big. 400 either way,
            // since none of them is the server's fault. Guarded on the content type because
            // InvalidDataException is a general I/O exception that other code can raise.
            //
            // No arm for a client that hangs up mid-upload, deliberately: since .NET 8
            // ExceptionHandlerMiddleware answers 499 itself when RequestAborted is cancelled and
            // the exception is an IOException or an OperationCanceledException, without calling any
            // IExceptionHandler — an arm here would be dead code, and a guard on cancellation alone
            // would be wrong anyway, because a NAS write failing after the browser gave up is an
            // IOException on an aborted request too.
            InvalidDataException when httpContext.Request.HasFormContentType =>
            (
                "The upload could not be read. It may be damaged, or larger than this server accepts.",
                "InvalidUpload",
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            // A body over the server's limit arrives here too, and answering it with "check your
            // input data types" sends the user to inspect a perfectly good file. Kestrel and IIS
            // both set StatusCode on the exception, so keep whatever they decided. Above the
            // general arm below, which would otherwise swallow it.
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
            (
                "The upload is larger than this server accepts.",
                "PayloadTooLarge",
                httpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge
            ),
            BadHttpRequestException badHttpEx =>
            (
                GetFriendlyDeserializationMessage(badHttpEx),
                "InvalidRequestFormat",
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            BulkUploadParseException =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            BadRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            DomainException =>
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

        if (exception is BulkUploadParseException bulkEx)
            problemDetails.Extensions.Add("rowErrors", bulkEx.RowErrors);

        if (exception is ConflictException { Code: not null } conflictEx)
            problemDetails.Extensions.Add("errorCode", conflictEx.Code);

        // Not the middleware's token: it hands us `RequestAborted`, which is already cancelled for
        // anything arising from a cancellation — the write would throw, this method would return
        // false, and the middleware would log a second error and rethrow what we just handled.
        // Writing to a socket that has gone away fails on its own terms.
        await httpContext.Response.WriteAsJsonAsync(problemDetails, CancellationToken.None);
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