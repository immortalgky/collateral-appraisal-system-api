using System.Text.Json;
using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features;

public class IncomeDisplaySeqValidatorTests
{
    private static IncomeAssumptionInput Assumption(int seq) =>
        new("I03", $"Row {seq}", "positive", seq, "03", JsonDocument.Parse("{}").RootElement);

    private static List<IncomeSectionInput> Sections(params int[] assumptionSeqs) =>
    [
        new("income", "Income", "positive", 0,
        [
            new("income", "Operating Income", "positive", 0, assumptionSeqs.Select(Assumption).ToList())
        ])
    ];

    [Fact]
    public void UniqueSeqs_Pass() =>
        IncomeDisplaySeqValidator.EnsureUnique(Sections(0, 1, 2));

    [Fact]
    public void DuplicateSeqAfterDeleteThenAdd_IsRejected()
    {
        // rows 0,1,2 → delete 1 → add one numbered rows.length (2) → 0,2,2
        var ex = Assert.Throws<BadRequestException>(() => IncomeDisplaySeqValidator.EnsureUnique(Sections(0, 2, 2)));
        Assert.Contains("Duplicate DisplaySeq 2", ex.Message);
    }
}
