using Appraisal.Application.Features.Invoices.BulkMarkInvoicesPaid;
using Appraisal.Application.Features.Invoices.CreateInvoice;
using Appraisal.Application.Features.Invoices.DeleteInvoice;
using Appraisal.Application.Features.Invoices.GetEligibleAssignments;
using Appraisal.Application.Features.Invoices.GetInvoiceById;
using Appraisal.Application.Features.Invoices.GetInvoiceList;
using Appraisal.Application.Features.Invoices.MarkInvoicePaid;
using Appraisal.Application.Features.Invoices.SubmitInvoice;
using Appraisal.Application.Features.Invoices.UpdateInvoiceDraft;
using Appraisal.Application.Features.Invoices.UpdateInvoiceNumber;

namespace Appraisal.Application.Features.Invoices;

public class InvoiceEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/invoices", async (
            ICurrentUserService currentUser,
            ISender sender,
            [FromQuery] int pageNumber = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? companySearch = null,
            [FromQuery] Guid? companyId = null,
            [FromQuery] DateOnly? sentDateFrom = null,
            [FromQuery] DateOnly? sentDateTo = null,
            [FromQuery] DateOnly? paidDateFrom = null,
            [FromQuery] DateOnly? paidDateTo = null,
            [FromQuery] string? search = null,
            [FromQuery] string? groupBy = null,
            CancellationToken ct = default) =>
        {
            var callerCompanyId = currentUser.CompanyId;
            var isInternal = currentUser.IsInRole("IntAdmin") || currentUser.IsInRole("Admin");

            if (callerCompanyId is null && !isInternal)
                return Results.Forbid();

            // External callers are forced to their own company. Internal admins
            // may filter by any company via the `companyId` query param (null = all).
            var effectiveCompanyId = isInternal ? companyId : callerCompanyId;

            var query = new GetInvoiceListQuery(
                pageNumber, pageSize, status, companySearch,
                sentDateFrom, sentDateTo, paidDateFrom, paidDateTo,
                search, effectiveCompanyId, groupBy);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetInvoices")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapGet("/invoices/eligible-assignments", async (
            ICurrentUserService currentUser,
            ISender sender,
            [FromQuery] string? searchAppraisalNo = null,
            [FromQuery] DateOnly? submittedDateFrom = null,
            [FromQuery] DateOnly? submittedDateTo = null,
            [FromQuery] Guid? currentInvoiceId = null,
            CancellationToken ct = default) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var query = new GetEligibleAssignmentsQuery(
                companyId, searchAppraisalNo, submittedDateFrom, submittedDateTo, currentInvoiceId);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetEligibleAssignments")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapGet("/invoices/{id:guid}", async (
            Guid id,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            var callerCompanyId = currentUser.CompanyId;
            var isInternal = currentUser.IsInRole("IntAdmin") || currentUser.IsInRole("Admin");

            if (callerCompanyId is null && !isInternal)
                return Results.Forbid();

            var query = new GetInvoiceByIdQuery(id, callerCompanyId);
            var result = await sender.Send(query, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetInvoiceById")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPost("/invoices", async (
            CreateInvoiceRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var command = new CreateInvoiceCommand(companyId, request.AssignmentIds, request.Notes);
            var id = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = id });
        })
        .WithName("CreateInvoice")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPut("/invoices/{id:guid}", async (
            Guid id,
            UpdateInvoiceDraftRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var command = new UpdateInvoiceDraftCommand(id, companyId, request.AssignmentIds, request.Notes);
            var invoiceId = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = invoiceId });
        })
        .WithName("UpdateInvoiceDraft")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPatch("/invoices/{id:guid}/number", async (
            Guid id,
            UpdateInvoiceNumberRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var command = new UpdateInvoiceNumberCommand(id, companyId, request.InvoiceNumber);
            var invoiceId = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = invoiceId });
        })
        .WithName("UpdateInvoiceNumber")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPost("/invoices/{id:guid}/submit", async (
            Guid id,
            SubmitInvoiceRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var command = new SubmitInvoiceCommand(id, companyId, request.InvoiceNumber);
            var invoiceId = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = invoiceId });
        })
        .WithName("SubmitInvoice")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapDelete("/invoices/{id:guid}", async (
            Guid id,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var command = new DeleteInvoiceCommand(id, companyId);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .WithName("DeleteInvoice")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPost("/invoices/{id:guid}/mark-paid", async (
            Guid id,
            MarkInvoicePaidRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            var isInternal = currentUser.IsInRole("IntAdmin") || currentUser.IsInRole("Admin");
            if (!isInternal) return Results.Forbid();

            var approvedBy = currentUser.Username ?? currentUser.UserId?.ToString();
            if (approvedBy is null) return Results.Forbid();

            var command = new MarkInvoicePaidCommand(id, approvedBy, request.PaymentOrderNo, request.PaidDate);
            var invoiceId = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = invoiceId });
        })
        .WithName("MarkInvoicePaid")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPost("/invoices/bulk-mark-paid", async (
            BulkMarkInvoicesPaidRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            var isInternal = currentUser.IsInRole("IntAdmin") || currentUser.IsInRole("Admin");
            if (!isInternal) return Results.Forbid();

            var approvedBy = currentUser.Username ?? currentUser.UserId?.ToString();
            if (approvedBy is null) return Results.Forbid();

            var command = new BulkMarkInvoicesPaidCommand(
                request.InvoiceIds,
                approvedBy,
                request.PaymentOrderNo,
                request.PaidDate);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .WithName("BulkMarkInvoicesPaid")
        .WithTags("Invoice")
        .RequireAuthorization();
    }
}

public record CreateInvoiceRequest(Guid[] AssignmentIds, string? Notes);
public record UpdateInvoiceDraftRequest(Guid[] AssignmentIds, string? Notes);
public record SubmitInvoiceRequest(string InvoiceNumber);
public record UpdateInvoiceNumberRequest(string InvoiceNumber);
public record MarkInvoicePaidRequest(string PaymentOrderNo, DateOnly PaidDate);
public record BulkMarkInvoicesPaidRequest(Guid[] InvoiceIds, string PaymentOrderNo, DateOnly PaidDate);
