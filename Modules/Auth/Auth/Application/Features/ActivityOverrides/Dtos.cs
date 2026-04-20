namespace Auth.Application.Features.ActivityOverrides;

public record ActivitySummaryDto(string ActivityId, string Label);

public record ActivityOverrideRowDto(
    Guid MenuItemId,
    string ItemKey,
    string Label,
    bool IsVisible,
    bool CanEdit,
    bool HasOverride);

public record ActivityOverridesResponse(
    string ActivityId,
    List<ActivityOverrideRowDto> Rows);

public record UpdateActivityOverridesRequest(
    List<UpdateActivityOverrideRow> Rows);

public record UpdateActivityOverrideRow(
    Guid MenuItemId,
    bool IsVisible,
    bool CanEdit);
