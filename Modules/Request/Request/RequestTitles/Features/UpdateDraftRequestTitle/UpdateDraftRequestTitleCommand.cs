using System;

namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public record UpdateDraftRequestTitleCommand(
    List<RequestTitlesCommandDto> RequestTitleCommandDtos
) : ICommand<UpdateDraftRequestTitleResult>;
