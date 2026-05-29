namespace Shared.Exceptions;

/// <summary>
/// Thrown when one or more rows in a bulk-upload Excel file fail validation.
/// Carries a list of row-level error objects so the caller can surface them to the user.
/// The handler maps this to HTTP 400 and adds the errors to the ProblemDetails extensions.
/// </summary>
public class BulkUploadParseException : Exception
{
    /// <summary>
    /// Row-level errors. Each item is an anonymous/record object that will be serialised
    /// directly into the 400 response under the key "rowErrors".
    /// </summary>
    public IReadOnlyList<object> RowErrors { get; }

    public BulkUploadParseException(IReadOnlyList<object> rowErrors)
        : base($"The uploaded file contains {rowErrors.Count} row error(s). Please fix them and try again.")
    {
        RowErrors = rowErrors;
    }
}
