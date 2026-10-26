using Appraisal.Infrastructure;
using Integration.Contracts.FileInterface;
using Integration.FileInterface.Format.Reappraisal;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Data;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// The dev COLLATREV generator end to end: the flag it is given lands at position 650 of EVERY row it
/// writes. The three appraisals are completed in 2099 so they are the newest, which is the order the
/// generator reads them in. The file goes to a temp directory, not the real reappraisal inbox, so the live
/// recurring job cannot pick it up.
/// </summary>
[Collection("Integration")]
public class CollatrevTestFileGeneratorTests(IntegrationTestFixture fixture)
{
    private static readonly DateOnly FileDate = new(2099, 12, 31);

    [Theory]
    [InlineData("Y", 'Y')]
    [InlineData("N", 'N')]
    [InlineData(null, ' ')]
    public async Task TheFlagLandsAtPosition650OfEveryRow(string? autoUpdate, char expected)
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ISqlConnectionFactory>();
        var db = sp.GetRequiredService<AppraisalDbContext>();

        for (var i = 0; i < 3; i++)
        {
            var requestId = await CollateralResultSeed.InsertRequestAsync(factory);
            db.Appraisals.Add(CollateralResultSeed.NewCompleted(requestId, new DateTime(2099, 1, 1 + i)));
        }
        await db.SaveChangesAsync();

        var inbox = Path.Combine(Path.GetTempPath(), $"collatrev-gen-{Guid.NewGuid():N}");
        var config = Substitute.For<IFileInterfaceConfigProvider>();
        config.GetAsync(FileInterfaceCodes.Reappraisal, Arg.Any<CancellationToken>()).Returns(
            new FileInterfaceConfig(FileInterfaceCodes.Reappraisal, "Inbound", null, null, null, inbox, null, null, true));
        var builder = new CollatrevTestFileBuilder(factory, config, new CollatrevFileWriter(),
            NullLogger<CollatrevTestFileBuilder>.Instance);

        try
        {
            var result = await builder.GenerateAsync(3, FileDate, autoUpdate);
            var details = (await File.ReadAllLinesAsync(result.FilePath))
                .Where(l => l.StartsWith('D')).ToList();

            Assert.Equal(3, details.Count);
            Assert.All(details, line =>
            {
                Assert.Equal(650, line.Length);
                Assert.Equal(expected, line[649]);
            });
        }
        finally
        {
            if (Directory.Exists(inbox))
                Directory.Delete(inbox, recursive: true);
        }
    }
}
