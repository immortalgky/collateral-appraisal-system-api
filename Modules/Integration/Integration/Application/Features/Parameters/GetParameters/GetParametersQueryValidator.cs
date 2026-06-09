using FluentValidation;

namespace Integration.Application.Features.Parameters.GetParameters;

public class GetParametersQueryValidator : AbstractValidator<GetParametersQuery>
{
    public GetParametersQueryValidator()
    {
        When(x => x.Groups is not null, () =>
        {
            RuleFor(x => x.Groups!.Count).LessThanOrEqualTo(50)
                .WithMessage("At most 50 parameter groups can be requested at once.");
            RuleForEach(x => x.Groups).NotEmpty().MaximumLength(100);
        });
    }
}
