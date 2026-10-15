using NSubstitute;
using Parameter;
using Parameter.DocumentRequirements.Features.CreateDocumentType;
using Parameter.DocumentRequirements.Models;
using Shared.Exceptions;

namespace Parameter.Tests.DocumentRequirements;

/// <summary>
/// A new document type does not carry its old files forward by default; only an explicit true turns it on,
/// and the locked summary codes still reject an explicit false.
/// </summary>
public class CreateDocumentTypeCarryForwardTests
{
    private static async Task<DocumentType> Create(string code, bool? flag)
    {
        var repository = Substitute.For<IDocumentRequirementRepository>();
        var handler = new CreateDocumentTypeCommandHandler(repository, Substitute.For<IParameterUnitOfWork>());

        await handler.Handle(new CreateDocumentTypeCommand(code, "n", null, null, CarryForwardByDefault: flag), default);

        return repository.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IDocumentRequirementRepository.AddDocumentType))
            .Select(c => (DocumentType)c.GetArguments()[0]!)
            .Single();
    }

    [Fact]
    public void The_model_default_is_dont_use() =>
        Assert.False(DocumentType.Create("D900", "n").CarryForwardByDefault);

    [Fact]
    public async Task An_omitted_flag_creates_a_dont_use_type() =>
        Assert.False((await Create("D901", null)).CarryForwardByDefault);

    [Fact]
    public async Task An_explicit_true_is_kept() =>
        Assert.True((await Create("D902", true)).CarryForwardByDefault);

    [Theory]
    [InlineData("D036")]
    [InlineData("d042")]
    [InlineData("D043 ")]
    public async Task A_locked_code_rejects_an_explicit_false(string code) =>
        await Assert.ThrowsAsync<BadRequestException>(() => Create(code, false));
}
