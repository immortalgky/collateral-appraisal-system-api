using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.ImportProjectUnitPrices;

/// <summary>An unreadable file is a 400 before any database access (there is no project here, which would be a 404).</summary>
public class UnitPriceImportHandlersRejectBadFilesFirstTests
{
    private static AppraisalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Preview_RejectsAGarbageFileBeforeLookingForTheProject()
    {
        await using var db = NewDb();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new PreviewUnitPriceImportQueryHandler(db).Handle(
                new PreviewUnitPriceImportQuery(Guid.NewGuid(), "x.xlsx", "not an xlsx"u8.ToArray()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Preview_WithAGoodFileButNoProject_IsNotFound()
    {
        await using var db = NewDb();
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: 1));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new PreviewUnitPriceImportQueryHandler(db).Handle(
                new PreviewUnitPriceImportQuery(Guid.NewGuid(), "x.xlsx", file.ToArray()),
                CancellationToken.None));
    }
}
