namespace Shared.Dtos;

public record LandTitleDocumentDetailDto(
    string TitleNo,
    string BookNo,
    string PageNo,
    string LandNo,
    string SurveyNo,
    string? SheetNo
);
