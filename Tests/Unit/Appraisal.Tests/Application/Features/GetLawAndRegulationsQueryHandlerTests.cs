using Appraisal.Application.Features.Appraisals.GetLawAndRegulations;
using Appraisal.Domain.Appraisals;
using NSubstitute;

namespace Appraisal.Tests.Application.Features;

public class GetLawAndRegulationsQueryHandlerTests
{
    [Fact]
    public async Task Items_are_ordered_by_header_code_then_creation_time()
    {
        var appraisalId = Guid.NewGuid();
        var t = new DateTime(2026, 1, 1, 0, 0, 0);

        LawAndRegulation Make(string code, int minute, string remark)
        {
            var r = LawAndRegulation.Create(appraisalId, code, remark);
            r.CreatedAt = t.AddMinutes(minute);
            return r;
        }

        var repo = Substitute.For<ILawAndRegulationRepository>();
        repo.GetByAppraisalIdWithImagesAsync(appraisalId, Arg.Any<CancellationToken>())
            .Returns([Make("02", 0, "a"), Make("01", 0, "b"), Make("05", 0, "c"), Make("01", -5, "d")]);

        var result = await new GetLawAndRegulationsQueryHandler(repo)
            .Handle(new GetLawAndRegulationsQuery(appraisalId), default);

        Assert.Equal(["d", "b", "a", "c"], result.Items.Select(i => i.Remark));
    }
}
