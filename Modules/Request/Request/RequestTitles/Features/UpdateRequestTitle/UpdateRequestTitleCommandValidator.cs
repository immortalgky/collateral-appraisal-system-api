namespace Request.RequestTitles.Features.UpdateRequestTitle;

public class UpdateRequestTitleCommandValidator : AbstractValidator<UpdateRequestTitleCommand>
{
    public UpdateRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("RequestId is required");
        
        // Validate
    }
}