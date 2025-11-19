namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestTitleCommandValidator : AbstractValidator<UpdateDraftRequestTitleCommand>
{
    public UpdateDraftRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("RequestId is required");
        
        // RuleForEach(x => x.RequestTitleCommandDtos)
            // .SetValidator(new AddRequestTitlesCommandDtoValidator());
    }
}
