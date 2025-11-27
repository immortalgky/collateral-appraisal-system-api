namespace Shared.Result;

public class Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public Dictionary<string, object> Metadata { get; }

    public Error(string code, string message, ErrorType type = ErrorType.Validation,
        Dictionary<string, object> metadata = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    public static Error Validation(string code, string message)
    {
        return new Error(code, message, ErrorType.Validation);
    }

    public static Error NotFound(string entityName, object id)
    {
        return new Error($"{entityName}NotFound", $"{entityName} with id {id} was not found", ErrorType.NotFound);
    }

    public static Error Conflict(string message)
    {
        return new Error("Conflict", message, ErrorType.Conflict);
    }

    public static Error Unauthorized(string message)
    {
        return new Error("Unauthorized", message, ErrorType.Unauthorized);
    }

    public static Error System(string message)
    {
        return new Error("SystemError", message, ErrorType.System);
    }

    public static Error External(string system, string message)
    {
        return new Error($"{system}Error", message, ErrorType.External);
    }
}