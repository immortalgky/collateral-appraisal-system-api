namespace Shared.Exceptions;

/// <summary>
/// A refusal by this application's own authorisation rules, whose message is safe — and useful —
/// to show the caller ("Exporting the appraisal list is not available on this permission.").
///
/// Exists because <see cref="UnauthorizedAccessException"/> cannot be used for that. The BCL throws
/// the same type from <c>System.IO</c> with the absolute path embedded in the message ("Access to
/// the path 'X' is denied."), and document upload/download sit on that path — so passing a caught
/// UnauthorizedAccessException's own message to the client would hand out the server's storage
/// layout. CustomExceptionHandler therefore answers UnauthorizedAccessException with a fixed line
/// and reserves the pass-through for this type.
///
/// Both map to 403.
/// </summary>
public class ForbiddenException(string message) : Exception(message);
