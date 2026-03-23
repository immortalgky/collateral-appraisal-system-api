namespace Parameter.ConstructionWork.Features.GetConstructionWorkGroups;

public record GetConstructionWorkGroupsResult(List<ConstructionWorkGroupDto> Groups);

public record ConstructionWorkGroupDto(
    Guid Id,
    string Code,
    string NameTh,
    string NameEn,
    int DisplayOrder,
    List<ConstructionWorkItemDto> Items
);

public record ConstructionWorkItemDto(
    Guid Id,
    string Code,
    string NameTh,
    string NameEn,
    int DisplayOrder
);
