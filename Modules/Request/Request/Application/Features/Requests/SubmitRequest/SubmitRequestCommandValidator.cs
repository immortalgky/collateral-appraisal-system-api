namespace Request.Application.Features.Requests.SubmitRequest;

public class SubmitRequestCommandValidator : AbstractValidator<SubmitRequestCommand>
{
    public SubmitRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Request Id is required.");
    }
}
