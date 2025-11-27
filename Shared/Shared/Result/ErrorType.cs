namespace Shared.Result;

public enum ErrorType
{
    Validation, // Business rule validation errors
    NotFound, // Entity not found
    Conflict, // Concurrency or duplicate errors
    Unauthorized, // Authorization errors
    System, // Technical/infrastructure errors
    External // External system errors (LOS, AS400, etc)
}