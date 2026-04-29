namespace Request.Application.Services;

public sealed record DocumentValidationInput(
    string? Purpose,
    IReadOnlyCollection<string> UploadedApplicationDocTypes,
    IReadOnlyList<TitleDocumentInput> Titles);

public sealed record TitleDocumentInput(
    string? CollateralType,
    IReadOnlyCollection<string> UploadedDocTypes);

public interface IRequestDocumentValidator
{
    Task ValidateAsync(DocumentValidationInput input, CancellationToken cancellationToken);
}
