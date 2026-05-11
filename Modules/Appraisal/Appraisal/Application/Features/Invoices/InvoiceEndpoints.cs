using Appraisal.Application.Features.Invoices.ApproveInvoice;
using Appraisal.Application.Features.Invoices.CreateInvoice;
using Appraisal.Application.Features.Invoices.GetEligibleAssignments;
using Appraisal.Application.Features.Invoices.GetInvoiceById;
using Appraisal.Application.Features.Invoices.GetInvoiceList;
using Appraisal.Application.Features.Invoices.SubmitInvoice;
using Appraisal.Application.Features.Invoices.UpdateInvoiceDraft;

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
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            CancellationToken ct = default) =>
        {
            var companyId = currentUser.CompanyId;
            var isInternal = currentUser.IsInRole("IntAdmin") || currentUser.IsInRole("Admin");

            if (companyId is null && !isInternal)
                return Results.Forbid();

            var query = new GetInvoiceListQuery(pageNumber, pageSize, status, companySearch, dateFrom, dateTo, companyId);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetInvoices")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapGet("/invoices/eligible-assignments", async (
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var query = new GetEligibleAssignmentsQuery(companyId);
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

        app.MapPost("/invoices/{id:guid}/submit", async (
            Guid id,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (currentUser.CompanyId is not { } companyId) return Results.Forbid();
            var command = new SubmitInvoiceCommand(id, companyId);
            var invoiceId = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = invoiceId });
        })
        .WithName("SubmitInvoice")
        .WithTags("Invoice")
        .RequireAuthorization();

        app.MapPost("/invoices/{id:guid}/approve", async (
            Guid id,
            ApproveInvoiceRequest request,
            ICurrentUserService currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            var isInternal = currentUser.IsInRole("IntAdmin") || currentUser.IsInRole("Admin");
            if (!isInternal) return Results.Forbid();

            var approvedBy = currentUser.Username ?? currentUser.UserId?.ToString();
            if (approvedBy is null) return Results.Forbid();

            var command = new ApproveInvoiceCommand(id, approvedBy, request.PaymentReference, request.PaymentMethod, request.PaymentDate);
            var invoiceId = await sender.Send(command, ct);
            return Results.Ok(new { InvoiceId = invoiceId });
        })
        .WithName("ApproveInvoice")
        .WithTags("Invoice")
        .RequireAuthorization();
    }
}

public record CreateInvoiceRequest(Guid[] AssignmentIds, string? Notes);
public record UpdateInvoiceDraftRequest(Guid[] AssignmentIds, string? Notes);
public record ApproveInvoiceRequest(string? PaymentReference, string? PaymentMethod, DateOnly? PaymentDate);
