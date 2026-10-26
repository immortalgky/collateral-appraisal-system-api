using System.Linq.Expressions;
using Appraisal.Domain.Appraisals;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Application.Features.Fees;

/// <summary>The input rules of an individual fee line, shared by the add and update commands.</summary>
internal static class FeeItemRules
{
    // AppraisalFeeItems.FeeCode / FeeDescription column lengths.
    private const int FeeCodeMaxLength = 20;
    private const int FeeDescriptionMaxLength = 200;

    /// <param name="currentCode">
    /// For an update: the code the item already has. A legacy item whose code is outside TypeOfFee must stay editable
    /// (amount, description) as long as its code is left alone, so the code is only checked when it is new or changes.
    /// </param>
    public static void Apply<T>(
        AbstractValidator<T> validator,
        IParameterLookupService parameterLookup,
        Expression<Func<T, string>> feeCode,
        Expression<Func<T, string>> feeDescription,
        Expression<Func<T, decimal>> feeAmount,
        Func<T, CancellationToken, Task<string?>>? currentCode = null)
    {
        validator.RuleFor(feeCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Fee code is required.")
            .MaximumLength(FeeCodeMaxLength)
            .MustAsync(async (command, code, ct) =>
                (currentCode is not null && await currentCode(command, ct) == code)
                || (await parameterLookup.GetValidCodesAsync(
                    new ParameterDto(null, AppraisalFee.FeeTypeParameterGroup, null, null, code, null, true, null), ct))
                .Contains(code))
            .WithMessage($"Fee code is not a valid {AppraisalFee.FeeTypeParameterGroup} code.");

        validator.RuleFor(feeDescription)
            .NotEmpty().WithMessage("Fee description is required.")
            .MaximumLength(FeeDescriptionMaxLength);

        validator.RuleFor(feeAmount)
            .GreaterThan(0m).WithMessage("Fee amount must be greater than zero.");
    }
}
