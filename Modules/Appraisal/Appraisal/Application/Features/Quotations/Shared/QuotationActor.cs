namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// The user/system the command is being executed on behalf of. Built at the endpoint edge
/// so handlers don't need to know whether the call came from a browser JWT or an
/// integration client_credentials token.
///
/// <para><see cref="Role"/> is the IDP role claim ("RequestMaker", "Admin", "IntAdmin", ...),
/// not the event-log display label ("RM" / "Admin"). The handler derives the display label
/// from the role claim so there's a single source of truth.</para>
/// </summary>
public record QuotationActor(string Username, string Role, Guid? UserId = null);
