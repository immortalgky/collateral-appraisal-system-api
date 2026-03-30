using Auth.Domain.Companies;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Workflow.AssigneeSelection.Services;
using Workflow.Data.Repository;
using Workflow.Services.Hashing;
using Xunit;

namespace Workflow.Tests.AssigneeSelection.Services;

public class CompanyRoundRobinServiceTests
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IGroupHashService _groupHashService;
    private readonly CompanyRoundRobinService _sut;

    public CompanyRoundRobinServiceTests()
    {
        _companyRepository = Substitute.For<ICompanyRepository>();
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _groupHashService = Substitute.For<IGroupHashService>();
        var logger = Substitute.For<ILogger<CompanyRoundRobinService>>();

        _groupHashService.GenerateGroupsHash(Arg.Any<List<string>>()).Returns("hash-123");
        _groupHashService.GenerateGroupsList(Arg.Any<List<string>>()).Returns("list-123");

        _sut = new CompanyRoundRobinService(
            _companyRepository,
            _assignmentRepository,
            _groupHashService,
            logger);
    }

    private static Company CreateCompany(Guid id, string name)
    {
        // Use the factory method, then we need the Id to match
        // Company.Create generates its own Id, so we create and use reflection or just rely on matching by name
        var company = Company.Create(name);
        // Set the Id via reflection since it's from Entity<Guid> base
        typeof(Company).BaseType!.GetProperty("Id")!.SetValue(company, id);
        return company;
    }

    // --- Unfiltered overload tests ---

    [Fact]
    public async Task SelectCompanyAsync_ActiveCompanies_ReturnsSelectedCompany()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var companies = new List<Company> { CreateCompany(companyId, "Alpha Corp") };

        _companyRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(companies);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(
                "CompanyRouting", "hash-123", Arg.Any<CancellationToken>())
            .Returns(companyId.ToString());

        // Act
        var result = await _sut.SelectCompanyAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CompanyId.Should().Be(companyId);
        result.CompanyName.Should().Be("Alpha Corp");
    }

    [Fact]
    public async Task SelectCompanyAsync_NoActiveCompanies_ReturnsFailure()
    {
        // Arrange
        _companyRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<Company>());

        // Act
        var result = await _sut.SelectCompanyAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active companies");
    }

    [Fact]
    public async Task SelectCompanyAsync_RoundRobinReturnsNull_ReturnsFailure()
    {
        // Arrange
        var companies = new List<Company> { CreateCompany(Guid.NewGuid(), "Beta Corp") };
        _companyRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(companies);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _sut.SelectCompanyAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no result");
    }

    [Fact]
    public async Task SelectCompanyAsync_SelectedIdNotInCompanies_ReturnsFailure()
    {
        // Arrange
        var companies = new List<Company> { CreateCompany(Guid.NewGuid(), "Gamma Corp") };
        _companyRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(companies);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid().ToString()); // unknown ID

        // Act
        var result = await _sut.SelectCompanyAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task SelectCompanyAsync_RepositoryThrows_ReturnsFailure()
    {
        // Arrange
        _companyRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        // Act
        var result = await _sut.SelectCompanyAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DB down");
    }

    // --- Loan type overload tests ---

    [Fact]
    public async Task SelectCompanyAsync_WithLoanType_FiltersCompanies()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var companies = new List<Company> { CreateCompany(companyId, "Loan Specialist") };

        _companyRepository.GetByLoanTypeAsync("Mortgage", activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(companies);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(companyId.ToString());

        // Act
        var result = await _sut.SelectCompanyAsync("Mortgage");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CompanyId.Should().Be(companyId);

        // Verify it called GetByLoanTypeAsync, not GetAllAsync
        await _companyRepository.Received(1)
            .GetByLoanTypeAsync("Mortgage", activeOnly: true, Arg.Any<CancellationToken>());
        await _companyRepository.DidNotReceive()
            .GetAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectCompanyAsync_WithLoanType_UsesLoanTypeGroupKey()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var companies = new List<Company> { CreateCompany(companyId, "Delta Corp") };

        _companyRepository.GetByLoanTypeAsync("Auto", activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(companies);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(companyId.ToString());

        // Act
        await _sut.SelectCompanyAsync("Auto");

        // Assert — group key should be "LoanType_Auto"
        _groupHashService.Received(1).GenerateGroupsHash(
            Arg.Is<List<string>>(list => list.Count == 1 && list[0] == "LoanType_Auto"));
    }

    [Fact]
    public async Task SelectCompanyAsync_WithLoanType_NoCompanies_ReturnsFailure()
    {
        // Arrange
        _companyRepository.GetByLoanTypeAsync("Rare", activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<Company>());

        // Act
        var result = await _sut.SelectCompanyAsync("Rare");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Rare");
    }
}
